import cv2
import numpy as np

from app.infrastructure.evidence_store import SKELETON_EDGES
from app.inference.detector import PoseDetection


def annotate_preview(frame: np.ndarray, detections: list[PoseDetection],
                     polygons: list[list[tuple[float, float]]]) -> np.ndarray:
    canvas = frame.copy()
    for polygon in polygons:
        vertices = np.array(polygon, dtype=np.int32).reshape((-1, 1, 2))
        cv2.polylines(canvas, [vertices], True, (255, 175, 35), 2)
        for x, y in polygon:
            cv2.circle(canvas, (round(x), round(y)), 4, (255, 175, 35), -1)
    for detection in detections:
        x, y, width, height = detection.bbox
        color = (0, 140, 255) if detection.posture == "SITTING" else (100, 210, 100)
        cv2.rectangle(canvas, (x, y), (x + width, y + height), color, 2)
        cv2.putText(canvas, f"{detection.posture} {detection.confidence:.2f}",
                    (x, max(20, y - 8)), cv2.FONT_HERSHEY_SIMPLEX, 0.6, color, 2)
        for first, second in SKELETON_EDGES:
            a, b = detection.skeleton[first], detection.skeleton[second]
            if a != [0, 0] and b != [0, 0]:
                cv2.line(canvas, tuple(a), tuple(b), (0, 255, 255), 2)
        for point in detection.skeleton:
            if point != [0, 0]:
                cv2.circle(canvas, tuple(point), 3, (0, 255, 255), -1)
    return canvas

