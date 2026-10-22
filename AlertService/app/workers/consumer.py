"""Read detector events, persist alerts once, then push live updates."""

import json
import logging
import os
import threading
import time
from datetime import datetime, timedelta
from uuid import uuid4

import httpx
from redis import Redis
from sqlalchemy import select
from sqlalchemy.orm import Session

from app.schemas import AlertRead
from app.store import Alert, Behavior, engine, now_utc
from app.ws import hub


LOG = logging.getLogger(__name__)
REDIS_URL = os.getenv("REDIS_URL", "redis://redis:6379/0")
redis_client = Redis.from_url(REDIS_URL, decode_responses=True, socket_timeout=5)
CURSOR_KEY = "alert:analytics:last_id"
CAMERA_URL = os.getenv("CAMERA_SERVICE_URL", "http://camera-service:5101").rstrip("/")


def camera_metadata(camera_id: str) -> dict:
    try:
        with httpx.Client(timeout=4) as client:
            response = client.get(f"{CAMERA_URL}/api/cameras/{camera_id}/runtime",
                                  headers={"X-Internal-Key": os.getenv("INTERNAL_API_KEY", "")})
            response.raise_for_status()
            return response.json()
    except httpx.HTTPError:
        LOG.warning("Camera metadata unavailable for %s; using camera ID", camera_id)
        return {"cameraName": camera_id, "placeId": None, "placeName": None}


def behavior_for(db: Session, code: str, version: str) -> Behavior | None:
    row = db.scalar(select(Behavior).where(Behavior.code == code, Behavior.version == version))
    if row:
        return row
    rows = db.scalars(select(Behavior).where(Behavior.code == code)).all()
    return max(rows, key=lambda item: int(item.version[1:])) if rows else None


class EventConsumer:
    def __init__(self) -> None:
        self.stop_event = threading.Event()
        self.thread = threading.Thread(target=self.run, name="alert-consumer", daemon=True)

    def start(self) -> None:
        self.thread.start()

    def stop(self) -> None:
        self.stop_event.set()
        self.thread.join(timeout=4)

    def process(self, message_id: str, payload: dict) -> None:
        if payload.get("event") != "lv.behavior.detected" or not payload.get("inZone"):
            return
        required = ("cameraId", "behaviorCode", "behaviorVersion", "confidence", "posture",
                    "bbox", "skeleton", "zoneId", "snapshotUrl", "detectedAt", "metrics")
        if any(key not in payload for key in required):
            LOG.warning("Malformed behavior event %s", message_id)
            return
        with Session(engine) as db:
            alert = db.scalar(select(Alert).where(Alert.source_event_id == message_id))
            new = alert is None
            if new:
                camera = camera_metadata(payload["cameraId"])
                behavior = behavior_for(db, payload["behaviorCode"], payload["behaviorVersion"])
                active = db.scalar(select(Behavior.id).where(
                    Behavior.code == payload["behaviorCode"], Behavior.is_active.is_(True)))
                if behavior and not active:
                    LOG.info("Disabled behavior %s ignored", behavior.code)
                    return
                name = behavior.name_vi if behavior else payload["behaviorCode"]
                severity = behavior.severity if behavior else "WARNING"
                camera_name = camera.get("cameraName") or payload["cameraId"]
                alert = Alert(
                    id=f"alert-{uuid4().hex[:12]}", source_event_id=message_id,
                    title=f"{name} - {camera_name}", camera_id=payload["cameraId"],
                    camera_name=camera_name, place_id=camera.get("placeId"), place_name=camera.get("placeName"),
                    behavior_code=payload["behaviorCode"], behavior_version=payload["behaviorVersion"],
                    behavior_name=name, severity=severity, zone_id=payload["zoneId"],
                    in_zone=True, confidence=payload["confidence"], posture=payload["posture"],
                    bbox=payload["bbox"], skeleton=payload["skeleton"], metrics=payload["metrics"],
                    snapshot_url=payload["snapshotUrl"], detected_at=datetime.fromisoformat(payload["detectedAt"]),
                )
                db.add(alert)
                db.commit()
                db.refresh(alert)
                item = AlertRead.model_validate(alert).model_dump(mode="json", by_alias=True)
                hub.broadcast_from_worker({"type": "NewAlert", "alert": item})
            if alert.notification_sent_at is None:
                redis_client.xadd("notifications:events", {"payload": json.dumps({
                    "event": "lv.alert.created", "alertId": alert.id, "severity": alert.severity,
                    "title": alert.title}, ensure_ascii=False)}, maxlen=100_000, approximate=True)
                alert.notification_sent_at = now_utc()
                db.commit()
            if alert.clip_requested_at is None:
                detected_at = alert.detected_at
                request = {"event": "lv.clip.requested", "alertId": alert.id, "cameraId": alert.camera_id,
                           "from": (detected_at - timedelta(seconds=6)).isoformat(),
                           "to": (detected_at + timedelta(seconds=10)).isoformat()}
                redis_client.xadd("vision:clip:jobs", {"payload": json.dumps(request)},
                                  maxlen=100_000, approximate=True)
                alert.clip_requested_at = now_utc()
                db.commit()

    def run(self) -> None:
        while not self.stop_event.is_set():
            try:
                cursor = redis_client.get(CURSOR_KEY) or "0-0"
                for _, messages in redis_client.xread({"analytics:events": cursor}, count=20, block=1000):
                    for message_id, fields in messages:
                        try:
                            self.process(message_id, json.loads(fields["payload"]))
                        except (KeyError, ValueError, TypeError):
                            LOG.exception("Invalid analytics event %s; skipping", message_id)
                        redis_client.set(CURSOR_KEY, message_id)
            except Exception:
                LOG.exception("Alert consumer failed; retrying")
                self.stop_event.wait(2)
