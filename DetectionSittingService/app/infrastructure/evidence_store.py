from datetime import datetime
from pathlib import Path
from uuid import uuid4

import cv2

from app.core.config import settings
from app.inference.detector import PoseDetection


ROOT = settings.evidence_dir
SKELETON_EDGES = [(5, 6), (5, 7), (7, 9), (6, 8), (8, 10), (5, 11),
                  (6, 12), (11, 12), (11, 13), (13, 15), (12, 14), (14, 16)]


def save_snapshot(frame, detection: PoseDetection, captured_at: datetime) -> str:
    canvas = frame.copy()
    x, y, width, height = detection.bbox
    cv2.rectangle(canvas, (x, y), (x + width, y + height), (0, 140, 255), 2)
    cv2.putText(canvas, f"PERSON_SITTING {detection.confidence:.2f}",
                (x, max(20, y - 8)), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 140, 255), 2)
    for first, second in SKELETON_EDGES:
        a, b = detection.skeleton[first], detection.skeleton[second]
        if a != [0, 0] and b != [0, 0]:
            cv2.line(canvas, tuple(a), tuple(b), (0, 255, 255), 2)
    for point in detection.skeleton:
        if point != [0, 0]:
            cv2.circle(canvas, tuple(point), 3, (0, 255, 255), -1)
    folder = captured_at.strftime("%Y-%m-%d")
    destination = ROOT / folder
    destination.mkdir(parents=True, exist_ok=True)
    filename = f"sitting-{captured_at.strftime('%H%M%S')}-{uuid4().hex[:8]}.jpg"
    path = destination / filename
    if not cv2.imwrite(str(path), canvas, [cv2.IMWRITE_JPEG_QUALITY, 88]):
        raise OSError("Không lưu được ảnh minh chứng")
    return f"/detect/sitting/evidence/{folder}/{filename}"


def evidence_file(relative: str) -> Path:
    path = (ROOT / relative).resolve()
    if not path.is_relative_to(ROOT) or not path.is_file():
        raise FileNotFoundError(relative)
    return path

