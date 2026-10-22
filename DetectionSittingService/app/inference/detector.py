from dataclasses import dataclass

import cv2
import numpy as np

from app.domain.posture import posture_metrics
from app.domain.zone import Point, point_in_polygon
from app.inference.model_loader import PoseModel


@dataclass(frozen=True)
class PoseDetection:
    bbox: list[int]
    skeleton: list[list[int]]
    confidence: float
    posture: str
    in_zone: bool
    zone_id: str | None
    torso_angle_deg: float
    hip_knee_angle_deg: float
    anchor: Point


class SittingDetector:
    def __init__(self, model: PoseModel | None = None) -> None:
        self.model = model or PoseModel()

    def infer(self, frame: np.ndarray, polygon: list[Point] | None = None,
              zone_id: str | None = None) -> list[PoseDetection]:
        prediction = self.model.predict(frame)
        if prediction.keypoints is None or prediction.boxes is None:
            return []
        output: list[PoseDetection] = []
        for box, confidence, person_joints in zip(
                prediction.boxes.xyxy.cpu().numpy(), prediction.boxes.conf.cpu().numpy(),
                prediction.keypoints.data.cpu().numpy()):
            x1, y1, x2, y2 = (int(round(float(value))) for value in box)
            joints: list[Point | None] = []
            skeleton: list[list[int]] = []
            for joint in person_joints:
                visible = len(joint) < 3 or joint[2] >= 0.3
                if visible and joint[0] > 0 and joint[1] > 0:
                    point = (float(joint[0]), float(joint[1]))
                    joints.append(point)
                    skeleton.append([round(point[0]), round(point[1])])
                else:
                    joints.append(None)
                    skeleton.append([0, 0])
            posture, torso_angle, hip_angle = posture_metrics(joints)
            hips = [joints[index] for index in (11, 12) if joints[index] is not None]
            center = ((sum(point[0] for point in hips) / len(hips)),
                      (sum(point[1] for point in hips) / len(hips))) if hips else ((x1 + x2) / 2, y2)
            output.append(PoseDetection(
                bbox=[x1, y1, max(0, x2 - x1), max(0, y2 - y1)], skeleton=skeleton,
                confidence=round(float(confidence), 4), posture=posture,
                in_zone=bool(polygon and point_in_polygon(center, polygon)), zone_id=zone_id,
                torso_angle_deg=torso_angle, hip_knee_angle_deg=hip_angle, anchor=center))
        return output


def decode_image(raw: bytes) -> np.ndarray:
    image = cv2.imdecode(np.frombuffer(raw, dtype=np.uint8), cv2.IMREAD_COLOR)
    if image is None:
        raise ValueError("Ảnh không đọc được")
    return image

