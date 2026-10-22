from datetime import datetime
from typing import Literal

from pydantic import Field, field_validator

from app.schemas.common import ApiModel


class ScenarioWrite(ApiModel):
    camera_id: str
    behavior_code: str = Field(pattern=r"^[A-Z][A-Z0-9_]+$", max_length=100)
    behavior_version: str = Field(default="v1", pattern=r"^v[1-9][0-9]*$")
    zone_id: str = Field(min_length=1, max_length=80)
    zone_polygon: list[tuple[float, float]] = Field(min_length=3)
    zone_mode: Literal["ANY", "ALL"] = "ANY"
    detector_url: str = Field(default="http://detection-sitting:8001/detect", pattern=r"^https?://", max_length=500)
    enabled: bool = True

    @field_validator("zone_polygon")
    @classmethod
    def valid_polygon(cls, points: list[tuple[float, float]]) -> list[tuple[float, float]]:
        if any(x < 0 or y < 0 for x, y in points):
            raise ValueError("Tọa độ vùng phải không âm")
        area2 = sum(x1 * y2 - x2 * y1 for (x1, y1), (x2, y2) in zip(points, points[1:] + points[:1]))
        if abs(area2) < 1e-6:
            raise ValueError("Vùng cần ít nhất ba điểm không thẳng hàng")
        return points


class ScenarioRead(ScenarioWrite):
    id: str
    created_at: datetime

