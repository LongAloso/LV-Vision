from datetime import datetime, timezone

from sqlalchemy import Boolean, DateTime, Float, ForeignKey, JSON, String, Text
from sqlalchemy.orm import DeclarativeBase, Mapped, mapped_column, relationship


class Base(DeclarativeBase):
    pass


def now_utc() -> datetime:
    return datetime.now(timezone.utc)


class Place(Base):
    __tablename__ = "places"
    __table_args__ = {"schema": "camera"}

    id: Mapped[str] = mapped_column(String(40), primary_key=True)
    name: Mapped[str] = mapped_column(String(160), unique=True, nullable=False)
    floor: Mapped[str | None] = mapped_column(String(80))
    description: Mapped[str | None] = mapped_column(String(500))
    plc_device: Mapped[str | None] = mapped_column(String(160))
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=now_utc)


class Camera(Base):
    __tablename__ = "cameras"
    __table_args__ = {"schema": "camera"}

    id: Mapped[str] = mapped_column(String(40), primary_key=True)
    name: Mapped[str] = mapped_column(String(160), nullable=False)
    rtsp_url: Mapped[str] = mapped_column(Text, nullable=False)
    place_id: Mapped[str | None] = mapped_column(ForeignKey("camera.places.id", ondelete="SET NULL"))
    is_active: Mapped[bool] = mapped_column(Boolean, default=True, nullable=False)
    fps: Mapped[float] = mapped_column(Float, default=0, nullable=False)
    target_fps: Mapped[float] = mapped_column(Float, default=10, nullable=False)
    p50_ms: Mapped[float] = mapped_column(Float, default=0, nullable=False)
    p95_ms: Mapped[float] = mapped_column(Float, default=0, nullable=False)
    worker_id: Mapped[str | None] = mapped_column(String(80))
    reference_frame_url: Mapped[str | None] = mapped_column(String(2048))
    status_updated_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=now_utc)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=now_utc)
    place: Mapped[Place | None] = relationship()


class Scenario(Base):
    __tablename__ = "scenarios"
    __table_args__ = {"schema": "camera"}

    id: Mapped[str] = mapped_column(String(40), primary_key=True)
    camera_id: Mapped[str] = mapped_column(ForeignKey("camera.cameras.id", ondelete="CASCADE"))
    behavior_code: Mapped[str] = mapped_column(String(100), nullable=False)
    behavior_version: Mapped[str] = mapped_column(String(20), default="v1", nullable=False)
    zone_id: Mapped[str] = mapped_column(String(80), nullable=False)
    zone_polygon: Mapped[list] = mapped_column(JSON, nullable=False)
    zone_mode: Mapped[str] = mapped_column(String(20), default="ANY", nullable=False)
    detector_url: Mapped[str] = mapped_column(String(500), nullable=False)
    enabled: Mapped[bool] = mapped_column(Boolean, default=True, nullable=False)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=now_utc)

