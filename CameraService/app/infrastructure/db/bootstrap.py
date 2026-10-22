from sqlalchemy import text

from app.core.security import decrypt_rtsp, encrypt_rtsp
from app.infrastructure.db.models import Base
from app.infrastructure.db.session import engine


def initialize() -> None:
    with engine.begin() as connection:
        connection.execute(text("CREATE SCHEMA IF NOT EXISTS camera"))
    Base.metadata.create_all(engine)
    with engine.begin() as connection:
        connection.execute(text("ALTER TABLE camera.cameras ALTER COLUMN rtsp_url TYPE TEXT"))
        for camera_id, value in connection.execute(text("SELECT id, rtsp_url FROM camera.cameras")).all():
            if value.startswith("enc:v1:"):
                decrypt_rtsp(value)
            else:
                connection.execute(
                    text("UPDATE camera.cameras SET rtsp_url=:url WHERE id=:id"),
                    {"id": camera_id, "url": encrypt_rtsp(value)},
                )

