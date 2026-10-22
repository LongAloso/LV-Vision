"""Durable notification inbox; read state is per Identity user."""

import os
from datetime import datetime, timezone

from sqlalchemy import DateTime, ForeignKey, String, create_engine, text
from sqlalchemy.orm import DeclarativeBase, Mapped, Session, mapped_column


def now_utc() -> datetime:
    return datetime.now(timezone.utc)


class Base(DeclarativeBase):
    pass


class Notification(Base):
    __tablename__ = "items"
    __table_args__ = {"schema": "notification"}

    id: Mapped[str] = mapped_column(String(50), primary_key=True)
    alert_id: Mapped[str] = mapped_column(String(50), unique=True, nullable=False)
    severity: Mapped[str] = mapped_column(String(20), nullable=False)
    title: Mapped[str] = mapped_column(String(320), nullable=False)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=now_utc, nullable=False)


class ReadReceipt(Base):
    __tablename__ = "read_receipts"
    __table_args__ = {"schema": "notification"}

    notification_id: Mapped[str] = mapped_column(ForeignKey("notification.items.id", ondelete="CASCADE"), primary_key=True)
    user_id: Mapped[str] = mapped_column(String(100), primary_key=True)
    read_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=now_utc, nullable=False)


engine = create_engine(os.environ["DATABASE_URL"], pool_pre_ping=True)


def initialize() -> None:
    with engine.begin() as connection:
        connection.execute(text("CREATE SCHEMA IF NOT EXISTS notification"))
    Base.metadata.create_all(engine)


def as_json(row: Notification, read: bool = False) -> dict:
    return {"id": row.id, "alertId": row.alert_id, "severity": row.severity,
            "title": row.title, "createdAt": row.created_at.isoformat(), "isRead": read}


def get_session():
    with Session(engine) as session:
        yield session
