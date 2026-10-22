import math

from app.domain.zone import Point


def angle_at(a: Point, center: Point, b: Point) -> float:
    u = (a[0] - center[0], a[1] - center[1])
    v = (b[0] - center[0], b[1] - center[1])
    length = math.hypot(*u) * math.hypot(*v)
    if length < 1e-6:
        return 180.0
    cosine = max(-1.0, min(1.0, (u[0] * v[0] + u[1] * v[1]) / length))
    return math.degrees(math.acos(cosine))


def posture_metrics(joints: list[Point | None]) -> tuple[str, float, float]:
    candidates: list[tuple[float, float]] = []
    for shoulder, hip, knee, ankle in ((5, 11, 13, 15), (6, 12, 14, 16)):
        if any(joints[index] is None for index in (shoulder, hip, knee, ankle)):
            continue
        s, h, k, a = (joints[index] for index in (shoulder, hip, knee, ankle))
        hip_angle = angle_at(s, h, k)
        knee_angle = angle_at(h, k, a)
        torso_angle = abs(math.degrees(math.atan2(s[1] - h[1], s[0] - h[0])))
        if hip_angle <= 125 and knee_angle <= 140:
            candidates.append((torso_angle, hip_angle))
    if candidates:
        torso, hip = min(candidates, key=lambda pair: pair[1])
        return "SITTING", round(torso, 1), round(hip, 1)
    return "OTHER", 0.0, 180.0

