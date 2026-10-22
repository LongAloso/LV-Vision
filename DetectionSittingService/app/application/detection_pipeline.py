from dataclasses import replace
from datetime import datetime, timedelta, timezone

import numpy as np

from app.api.schemas import DetectionResult
from app.domain.events import BehaviorDetected
from app.domain.zone import point_in_polygon
from app.inference.detector import PoseDetection
from app.infrastructure.evidence_store import save_snapshot
from app.infrastructure.redis_publisher import publish_behavior


VIETNAM = timezone(timedelta(hours=7))


def process_detections(frame: np.ndarray, detections: list[PoseDetection], *,
                       camera_id: str, zone_id: str, polygon: list[tuple[float, float]],
                       behavior_version: str = "v1", publish: bool = False,
                       motion_px: float = 0, save_preview: bool = True) -> list[DetectionResult]:
    results: list[DetectionResult] = []
    captured_at = datetime.now(VIETNAM)
    for original in detections:
        detected = replace(original, in_zone=point_in_polygon(original.anchor, polygon), zone_id=zone_id)
        active = detected.posture == "SITTING" and detected.in_zone
        snapshot_url = save_snapshot(frame, detected, captured_at) if active and (publish or save_preview) else None
        result = DetectionResult(
            behaviorCode="PERSON_SITTING" if detected.posture == "SITTING" else "OTHER",
            posture=detected.posture, confidence=detected.confidence, bbox=detected.bbox,
            skeleton=detected.skeleton, inZone=detected.in_zone, zoneId=zone_id,
            metrics={"torsoAngleDeg": detected.torso_angle_deg,
                     "hipKneeAngleDeg": detected.hip_knee_angle_deg, "motionPx": motion_px},
            snapshotUrl=snapshot_url)
        results.append(result)
        if active and publish:
            publish_behavior(BehaviorDetected(
                cameraId=camera_id, behaviorCode="PERSON_SITTING",
                behaviorVersion=behavior_version, confidence=detected.confidence,
                posture="SITTING", bbox=detected.bbox, skeleton=detected.skeleton,
                zoneId=zone_id, inZone=True, snapshotUrl=snapshot_url,
                detectedAt=captured_at,
                metrics={**result.metrics, "zoneMode": "ANY", "template": "POSTURE_STATE"}))
    return results

