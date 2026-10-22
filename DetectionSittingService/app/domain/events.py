from datetime import datetime
from typing import Any

from pydantic import BaseModel, ConfigDict, Field


class BehaviorDetected(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    event: str = "lv.behavior.detected"
    cameraId: str
    behaviorCode: str
    behaviorVersion: str
    confidence: float = Field(ge=0, le=1)
    posture: str
    bbox: list[int] = Field(min_length=4, max_length=4)
    skeleton: list[list[int]]
    zoneId: str
    inZone: bool
    snapshotUrl: str
    detectedAt: datetime
    metrics: dict[str, Any]

