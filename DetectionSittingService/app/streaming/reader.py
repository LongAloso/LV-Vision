import logging
import math
import os
import threading
import time
from collections import deque

import cv2
import httpx
import numpy as np

from app.application.detection_pipeline import process_detections
from app.application.scenario_runtime import active_sitting_scenarios, target_fps
from app.inference.detector import SittingDetector
from app.infrastructure.camera_client import CameraServiceClient
from app.streaming.buffer import FrameBuffer
from app.streaming.preview import annotate_preview


LOG = logging.getLogger(__name__)


class StreamWorker:
    def __init__(self, camera_id: str, runtime: dict, detector: SittingDetector,
                 camera_client: CameraServiceClient) -> None:
        self.camera_id = camera_id
        self.runtime = runtime
        self.detector = detector
        self.camera_client = camera_client
        self.stop_event = threading.Event()
        self.buffer = FrameBuffer()
        self.preview_jpeg: bytes | None = None
        self.preview_lock = threading.Lock()
        self.connected = False
        self.thread = threading.Thread(target=self.run, name=f"camera-{camera_id}", daemon=True)
        self.last_event: dict[str, float] = {}
        self.previous_anchor: dict[str, tuple[float, float]] = {}

    def start(self) -> None:
        self.thread.start()

    def stop(self) -> None:
        self.stop_event.set()
        self.thread.join(timeout=8)

    def latest_preview(self) -> bytes | None:
        with self.preview_lock:
            annotated = self.preview_jpeg
        return annotated if annotated is not None else self.buffer.latest()

    def update_status(self, fps: float, p50: float, p95: float, active: bool) -> None:
        worker_id = f"sitting-{os.getenv('HOSTNAME', 'worker')}" if active else None
        try:
            self.camera_client.update_status(self.camera_id, {
                "fps": round(fps, 1), "p50Ms": round(p50, 1), "p95Ms": round(p95, 1),
                "workerId": worker_id, "isActive": active,
            })
        except httpx.HTTPError:
            LOG.warning("Cannot update camera status for %s", self.camera_id)

    def run(self) -> None:
        fps = target_fps(self.runtime)
        interval = 1 / fps
        stream_url = self.runtime["rtspUrl"]
        scenarios = active_sitting_scenarios(self.runtime)
        while not self.stop_event.is_set():
            capture = cv2.VideoCapture(stream_url)
            if not capture.isOpened():
                self.connected = False
                LOG.warning("RTSP camera %s unavailable; retrying", self.camera_id)
                self.update_status(0, 0, 0, False)
                capture.release()
                self.stop_event.wait(2)
                continue
            self._consume(capture, interval, scenarios)
            self.stop_event.wait(2)

    def _consume(self, capture: cv2.VideoCapture, interval: float, scenarios: list[dict]) -> None:
        durations: deque[float] = deque(maxlen=100)
        processed = 0
        window_started = time.monotonic()
        next_sample = 0.0
        last_preview = 0.0
        try:
            while not self.stop_event.is_set():
                ok, frame = capture.read()
                if not ok:
                    break
                self.connected = True
                now = time.monotonic()
                if next_sample == 0.0:
                    next_sample = now
                if now < next_sample - 0.02:
                    continue
                next_sample = max(next_sample + interval, now + 0.02)
                self.buffer.add(time.time(), frame)
                started = time.perf_counter()
                detections = self.detector.infer(frame) if scenarios else []
                self._process_scenarios(frame, detections, scenarios, now)
                if scenarios and now - last_preview >= interval:
                    self._update_preview(frame, detections, scenarios)
                    last_preview = now
                durations.append((time.perf_counter() - started) * 1000)
                processed += 1
                if now - window_started >= 5:
                    measured_fps = processed / (now - window_started)
                    self.update_status(measured_fps, float(np.percentile(durations, 50)),
                                       float(np.percentile(durations, 95)), True)
                    window_started = now
                    processed = 0
        except Exception:
            LOG.exception("Camera worker %s failed; reconnecting", self.camera_id)
        finally:
            self.connected = False
            capture.release()
            self.update_status(0, 0, 0, False)

    def _process_scenarios(self, frame, detections: list, scenarios: list[dict], now: float) -> None:
        for scenario in scenarios:
            key = scenario["id"]
            polygon = [tuple(point) for point in scenario["zonePolygon"]]
            can_publish = now - self.last_event.get(key, 0) >= 5
            anchor = next((item.anchor for item in detections if item.posture == "SITTING"), None)
            previous = self.previous_anchor.get(key)
            motion_px = math.dist(anchor, previous) if anchor and previous else 0.0
            if anchor:
                self.previous_anchor[key] = anchor
            results = process_detections(
                frame, detections, camera_id=self.camera_id, zone_id=scenario["zoneId"],
                polygon=polygon, behavior_version=scenario["behaviorVersion"],
                publish=can_publish, save_preview=False, motion_px=round(motion_px, 1))
            if can_publish and any(result.posture == "SITTING" and result.inZone for result in results):
                self.last_event[key] = now

    def _update_preview(self, frame, detections: list, scenarios: list[dict]) -> None:
        annotated = annotate_preview(
            frame, detections,
            [[tuple(point) for point in scenario["zonePolygon"]] for scenario in scenarios])
        encoded, preview = cv2.imencode(".jpg", annotated, [cv2.IMWRITE_JPEG_QUALITY, 85])
        if encoded:
            with self.preview_lock:
                self.preview_jpeg = preview.tobytes()

