import json
import logging
import threading
import time
from datetime import datetime, timedelta, timezone
from uuid import uuid4

import cv2
import numpy as np

from app.infrastructure.evidence_store import ROOT
from app.infrastructure.redis_publisher import redis_client
from app.streaming.manager import StreamManager


LOG = logging.getLogger(__name__)


class ClipWorker:
    def __init__(self, streams: StreamManager) -> None:
        self.streams = streams
        self.stop_event = threading.Event()
        self.thread = threading.Thread(target=self.run, name="clip-worker", daemon=True)

    def start(self) -> None:
        self.thread.start()

    def stop(self) -> None:
        self.stop_event.set()
        self.thread.join(timeout=4)

    def run(self) -> None:
        last_id = "$"
        while not self.stop_event.is_set():
            try:
                for _, messages in redis_client.xread({"vision:clip:jobs": last_id}, count=10, block=1000):
                    for message_id, fields in messages:
                        last_id = message_id
                        job = json.loads(fields["payload"])
                        if job.get("event") != "lv.clip.requested":
                            continue
                        result = {"event": "lv.clip.done", "alertId": job["alertId"],
                                  "clipUrl": self.cut(job)}
                        redis_client.xadd("vision:clip:jobs", {"payload": json.dumps(result)},
                                          maxlen=100_000, approximate=True)
            except Exception:
                LOG.exception("Clip worker failed; retrying")
                self.stop_event.wait(2)

    def cut(self, job: dict) -> str | None:
        stream = self.streams.workers.get(job["cameraId"])
        if stream is None:
            return None
        start = datetime.fromisoformat(job["from"]).timestamp()
        end = datetime.fromisoformat(job["to"]).timestamp()
        if end <= start or end - start > 30:
            return None
        if end > time.time():
            self.stop_event.wait(min(end - time.time(), 30))
        frames = stream.buffer.between(start, end)
        if len(frames) < 2:
            return None
        first = cv2.imdecode(np.frombuffer(frames[0][1], dtype=np.uint8), cv2.IMREAD_COLOR)
        if first is None:
            return None
        folder = datetime.fromtimestamp(start, timezone(timedelta(hours=7))).strftime("%Y-%m-%d")
        destination = ROOT / folder
        destination.mkdir(parents=True, exist_ok=True)
        filename = f"clip-{uuid4().hex[:12]}.mp4"
        path = destination / filename
        duration = max(0.001, frames[-1][0] - frames[0][0])
        fps = min(30.0, max(1.0, len(frames) / duration))
        height, width = first.shape[:2]
        writer = cv2.VideoWriter(str(path), cv2.VideoWriter_fourcc(*"mp4v"), fps, (width, height))
        if not writer.isOpened():
            return None
        try:
            for _, raw in frames:
                image = cv2.imdecode(np.frombuffer(raw, dtype=np.uint8), cv2.IMREAD_COLOR)
                if image is not None:
                    writer.write(image)
        finally:
            writer.release()
        return f"/detect/sitting/evidence/{folder}/{filename}"

