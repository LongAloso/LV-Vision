"""Environment-backed settings used by CameraService."""

import os
from dataclasses import dataclass


@dataclass(frozen=True)
class Settings:
    database_url: str
    identity_url: str
    jwt_key: str
    jwt_issuer: str
    jwt_audience: str
    internal_api_key: str
    camera_rtsp_key: str


def load_settings() -> Settings:
    return Settings(
        database_url=os.environ["DATABASE_URL"],
        identity_url=os.getenv("IDENTITY_URL", "http://identityservice:5000").rstrip("/"),
        jwt_key=os.environ["JWT_KEY"],
        jwt_issuer=os.environ["JWT_ISSUER"],
        jwt_audience=os.environ["JWT_AUDIENCE"],
        internal_api_key=os.getenv("INTERNAL_API_KEY", ""),
        camera_rtsp_key=os.environ["CAMERA_RTSP_KEY"],
    )


settings = load_settings()

