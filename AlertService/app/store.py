"""Alert-owned PostgreSQL tables; detector services know only the wire contract."""

import os
from collections.abc import Generator
from datetime import datetime, timezone

from sqlalchemy import Boolean, DateTime, Float, ForeignKey, Index, Integer, JSON, String, Text, UniqueConstraint, create_engine, select, text
from sqlalchemy.orm import DeclarativeBase, Mapped, Session, mapped_column, relationship


def now_utc() -> datetime:
    return datetime.now(timezone.utc)


class Base(DeclarativeBase):
    pass


class Behavior(Base):
    __tablename__ = "behaviors"
    __table_args__ = (UniqueConstraint("code", "version", name="uq_behavior_code_version"),
                      {"schema": "alert"})

    id: Mapped[str] = mapped_column(String(100), primary_key=True)
    code: Mapped[str] = mapped_column(String(100), nullable=False)
    version: Mapped[str] = mapped_column(String(20), nullable=False)
    name_vi: Mapped[str] = mapped_column(String(160), nullable=False)
    name_en: Mapped[str] = mapped_column(String(160), nullable=False)
    detect_template: Mapped[str] = mapped_column(String(80), nullable=False)
    severity: Mapped[str] = mapped_column(String(20), nullable=False)
    zone_type: Mapped[str] = mapped_column(String(80), nullable=False)
    parameters: Mapped[dict] = mapped_column(JSON, default=dict, nullable=False)
    is_active: Mapped[bool] = mapped_column(Boolean, default=True, nullable=False)
    is_system: Mapped[bool] = mapped_column(Boolean, default=False, nullable=False)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=now_utc)


class Alert(Base):
    __tablename__ = "alerts"
    __table_args__ = (Index("ix_alert_detected_at", "detected_at"),
                      Index("ix_alert_filters", "status", "camera_id", "behavior_code"),
                      {"schema": "alert"})

    id: Mapped[str] = mapped_column(String(50), primary_key=True)
    source_event_id: Mapped[str] = mapped_column(String(100), unique=True, nullable=False)
    title: Mapped[str] = mapped_column(String(320), nullable=False)
    camera_id: Mapped[str] = mapped_column(String(80), nullable=False)
    camera_name: Mapped[str] = mapped_column(String(160), nullable=False)
    place_id: Mapped[str | None] = mapped_column(String(80))
    place_name: Mapped[str | None] = mapped_column(String(160))
    behavior_code: Mapped[str] = mapped_column(String(100), nullable=False)
    behavior_version: Mapped[str] = mapped_column(String(20), nullable=False)
    behavior_name: Mapped[str] = mapped_column(String(160), nullable=False)
    severity: Mapped[str] = mapped_column(String(20), nullable=False)
    zone_id: Mapped[str] = mapped_column(String(80), nullable=False)
    in_zone: Mapped[bool] = mapped_column(Boolean, nullable=False)
    confidence: Mapped[float] = mapped_column(Float, nullable=False)
    posture: Mapped[str] = mapped_column(String(60), nullable=False)
    bbox: Mapped[list] = mapped_column(JSON, nullable=False)
    skeleton: Mapped[list] = mapped_column(JSON, nullable=False)
    metrics: Mapped[dict] = mapped_column(JSON, nullable=False)
    snapshot_url: Mapped[str] = mapped_column(String(2048), nullable=False)
    clip_url: Mapped[str | None] = mapped_column(String(2048))
    clip_status: Mapped[str] = mapped_column(String(20), default="PENDING", nullable=False)
    detected_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), nullable=False)
    status: Mapped[str] = mapped_column(String(30), default="NEW", nullable=False)
    assignee: Mapped[str | None] = mapped_column(String(160))
    reason: Mapped[str | None] = mapped_column(String(1000))
    notification_sent_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True))
    clip_requested_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True))
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=now_utc)
    updated_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=now_utc, onupdate=now_utc)
    remediations: Mapped[list["Remediation"]] = relationship(back_populates="alert", cascade="all, delete-orphan")


class Remediation(Base):
    __tablename__ = "remediations"
    __table_args__ = {"schema": "alert"}

    id: Mapped[str] = mapped_column(String(50), primary_key=True)
    alert_id: Mapped[str] = mapped_column(ForeignKey("alert.alerts.id", ondelete="CASCADE"), nullable=False)
    description: Mapped[str] = mapped_column(Text, nullable=False)
    person: Mapped[str] = mapped_column(String(160), nullable=False)
    result: Mapped[str] = mapped_column(String(500), nullable=False)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=now_utc)
    alert: Mapped[Alert] = relationship(back_populates="remediations")


engine = create_engine(os.environ["DATABASE_URL"], pool_pre_ping=True)


def initialize() -> None:
    with engine.begin() as connection:
        connection.execute(text("CREATE SCHEMA IF NOT EXISTS alert"))
    Base.metadata.create_all(engine)
    with engine.begin() as connection:
        connection.execute(text("ALTER TABLE alert.alerts ADD COLUMN IF NOT EXISTS clip_status VARCHAR(20) NOT NULL DEFAULT 'PENDING'"))
    with Session(engine) as session:
        for version, parameters in (("v1", {"hipMaxDeg": 130, "kneeMaxDeg": 145}),
                                    ("v2", {"hipMaxDeg": 125, "kneeMaxDeg": 140})):
            code = "PERSON_SITTING"
            if session.scalar(select(Behavior.id).where(Behavior.code == code, Behavior.version == version)):
                continue
            session.add(Behavior(id=f"beh-person-sitting-{version}", code=code, version=version,
                                 name_vi="Người đang ngồi", name_en="Person sitting",
                                 detect_template="POSTURE_STATE", severity="WARNING",
                                 zone_type="Lối đi", parameters=parameters, is_system=True))
        session.commit()


def get_session() -> Generator[Session, None, None]:
    with Session(engine) as session:
        yield session
