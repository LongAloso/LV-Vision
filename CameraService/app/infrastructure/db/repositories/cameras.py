from sqlalchemy import func, select
from sqlalchemy.orm import Session

from app.infrastructure.db.models import Camera, Scenario


def get(db: Session, camera_id: str) -> Camera | None:
    return db.get(Camera, camera_id)


def page(db: Session, offset: int, limit: int) -> tuple[int, list[Camera]]:
    total = db.scalar(select(func.count()).select_from(Camera)) or 0
    rows = list(db.scalars(select(Camera).order_by(Camera.created_at, Camera.id)
                           .offset(offset).limit(limit)).all())
    return total, rows


def all_by_name(db: Session) -> list[Camera]:
    return list(db.scalars(select(Camera).order_by(Camera.name)).all())


def add(db: Session, camera: Camera) -> Camera:
    db.add(camera)
    return camera


def remove(db: Session, camera: Camera) -> None:
    db.delete(camera)


def enabled_scenarios(db: Session, camera_id: str) -> list[Scenario]:
    return list(db.scalars(select(Scenario).where(
        Scenario.camera_id == camera_id, Scenario.enabled.is_(True))).all())

