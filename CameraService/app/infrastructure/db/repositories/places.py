from sqlalchemy import func, select
from sqlalchemy.orm import Session

from app.infrastructure.db.models import Place


def get(db: Session, place_id: str) -> Place | None:
    return db.get(Place, place_id)


def by_name(db: Session, name: str, exclude_id: str | None = None) -> Place | None:
    query = select(Place).where(Place.name == name)
    if exclude_id:
        query = query.where(Place.id != exclude_id)
    return db.scalar(query)


def page(db: Session, offset: int, limit: int) -> tuple[int, list[Place]]:
    total = db.scalar(select(func.count()).select_from(Place)) or 0
    rows = list(db.scalars(select(Place).order_by(Place.name).offset(offset).limit(limit)).all())
    return total, rows


def add(db: Session, place: Place) -> Place:
    db.add(place)
    return place


def remove(db: Session, place: Place) -> None:
    db.delete(place)

