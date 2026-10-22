"""Attach completed MP4 evidence to alerts and notify WebSocket clients."""

import json
import logging
import threading

from sqlalchemy.orm import Session

from app.schemas import AlertRead
from app.store import Alert, engine
from app.workers.consumer import redis_client
from app.ws import hub


LOG = logging.getLogger(__name__)
CURSOR_KEY = "alert:clip:last_id"


class ClipResultConsumer:
    def __init__(self) -> None:
        self.stop_event = threading.Event()
        self.thread = threading.Thread(target=self.run, name="clip-result-consumer", daemon=True)

    def start(self) -> None:
        self.thread.start()

    def stop(self) -> None:
        self.stop_event.set()
        self.thread.join(timeout=4)

    def process(self, payload: dict) -> None:
        if payload.get("event") != "lv.clip.done" or "clipUrl" not in payload:
            return
        with Session(engine) as db:
            alert = db.get(Alert, payload.get("alertId"))
            status = "READY" if payload["clipUrl"] else "UNAVAILABLE"
            if alert is None or (alert.clip_url == payload["clipUrl"] and alert.clip_status == status):
                return
            alert.clip_url = payload["clipUrl"]
            alert.clip_status = status
            db.commit()
            db.refresh(alert)
            item = AlertRead.model_validate(alert).model_dump(mode="json", by_alias=True)
        hub.broadcast_from_worker({"type": "ClipReady", "alert": item})

    def run(self) -> None:
        while not self.stop_event.is_set():
            try:
                cursor = redis_client.get(CURSOR_KEY) or "0-0"
                for _, messages in redis_client.xread({"vision:clip:jobs": cursor}, count=20, block=1000):
                    for message_id, fields in messages:
                        try:
                            self.process(json.loads(fields["payload"]))
                        except (KeyError, ValueError, TypeError):
                            LOG.exception("Invalid clip message %s; skipping", message_id)
                        redis_client.set(CURSOR_KEY, message_id)
            except Exception:
                LOG.exception("Clip result consumer failed; retrying")
                self.stop_event.wait(2)
