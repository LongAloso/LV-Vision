import os
from dataclasses import dataclass
from pathlib import Path


@dataclass(frozen=True)
class Settings:
    model_path: str
    model_device: str
    model_image_size: int
    camera_service_url: str
    identity_url: str
    redis_url: str
    evidence_dir: Path
    internal_api_key: str
    jwt_key: str
    jwt_issuer: str
    jwt_audience: str
    stream_capacity: int


settings = Settings(
    model_path=os.getenv("MODEL_PATH", "/models/model.pt"),
    model_device=os.getenv("MODEL_DEVICE", "cpu"),
    model_image_size=int(os.getenv("MODEL_IMGSZ", "416")),
    camera_service_url=os.getenv("CAMERA_SERVICE_URL", "http://camera-service:5101").rstrip("/"),
    identity_url=os.getenv("IDENTITY_URL", "http://identityservice:5000").rstrip("/"),
    redis_url=os.getenv("REDIS_URL", "redis://redis:6379/0"),
    evidence_dir=Path(os.getenv("EVIDENCE_DIR", "/data/evidence")).resolve(),
    internal_api_key=os.getenv("INTERNAL_API_KEY", ""),
    jwt_key=os.getenv("JWT_KEY", ""),
    jwt_issuer=os.getenv("JWT_ISSUER", ""),
    jwt_audience=os.getenv("JWT_AUDIENCE", ""),
    stream_capacity=int(os.getenv("STREAM_CAPACITY", "2")),
)

