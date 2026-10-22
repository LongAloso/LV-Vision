from datetime import datetime

from pydantic import Field

from app.schemas.common import ApiModel


class CameraWrite(ApiModel):
    name: str = Field(min_length=1, max_length=160)
    rtsp_url: str = Field(pattern=r"^rtsps?://", max_length=2048)
    place_id: str | None = None
    is_active: bool = True
    target_fps: float = Field(default=10, ge=1, le=60)


class CameraPatch(ApiModel):
    name: str | None = Field(default=None, min_length=1, max_length=160)
    rtsp_url: str | None = Field(default=None, pattern=r"^rtsps?://", max_length=2048)
    place_id: str | None = None
    target_fps: float | None = Field(default=None, ge=1, le=60)


class CameraRead(ApiModel):
    id: str
    name: str
    place_id: str | None
    is_active: bool
    target_fps: float
    fps: float
    p50_ms: float
    p95_ms: float
    worker_id: str | None
    reference_frame_url: str | None
    status_updated_at: datetime
    created_at: datetime


class CameraStatus(ApiModel):
    camera_id: str
    is_active: bool
    fps: float
    target_fps: float
    p50_ms: float
    p95_ms: float
    worker_id: str | None
    status_updated_at: datetime
    reference_frame_url: str | None


class CameraStatusUpdate(ApiModel):
    fps: float = Field(ge=0, le=120)
    p50_ms: float = Field(ge=0)
    p95_ms: float = Field(ge=0)
    worker_id: str | None = Field(default=None, max_length=80)
    is_active: bool = True
    reference_frame_url: str | None = Field(default=None, max_length=2048)

