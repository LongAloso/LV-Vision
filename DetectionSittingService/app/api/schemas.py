from pydantic import BaseModel, Field

from app.domain.zone import valid_polygon


class StreamStart(BaseModel):
    cameraId: str = Field(min_length=1)


class StreamStop(BaseModel):
    cameraId: str = Field(min_length=1)


class DetectionResult(BaseModel):
    behaviorCode: str
    posture: str
    confidence: float
    bbox: list[int]
    skeleton: list[list[int]]
    inZone: bool
    zoneId: str | None
    metrics: dict[str, float]
    snapshotUrl: str | None = None


def parse_polygon(raw: list[list[float]]) -> list[tuple[float, float]]:
    points = [tuple(point) for point in raw]
    if not valid_polygon(points):
        raise ValueError("Polygon cần ít nhất ba điểm không thẳng hàng, tọa độ không âm")
    return points

