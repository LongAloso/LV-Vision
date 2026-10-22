from uuid import uuid4

from fastapi import HTTPException
from sqlalchemy.orm import Session

from app.infrastructure.db.models import Place
from app.infrastructure.db.repositories import places
from app.schemas import Page, PlaceRead, PlaceWrite


def get_place(db: Session, place_id: str) -> Place:
    place = places.get(db, place_id)
    if place is None:
        raise HTTPException(404, f"Không tìm thấy địa điểm {place_id}")
    return place


def list_places(db: Session, page: int, page_size: int) -> Page:
    total, rows = places.page(db, (page - 1) * page_size, page_size)
    return Page(items=[PlaceRead.model_validate(row).model_dump(mode="json", by_alias=True) for row in rows],
                page=page, page_size=page_size, total=total)


def create_place(db: Session, body: PlaceWrite) -> Place:
    if places.by_name(db, body.name):
        raise HTTPException(409, "Địa điểm đã tồn tại")
    place = Place(id=f"place-{uuid4().hex[:10]}", **body.model_dump())
    places.add(db, place)
    db.commit()
    db.refresh(place)
    return place


def update_place(db: Session, place_id: str, body: PlaceWrite) -> Place:
    place = get_place(db, place_id)
    if places.by_name(db, body.name, exclude_id=place_id):
        raise HTTPException(409, "Địa điểm đã tồn tại")
    for key, value in body.model_dump().items():
        setattr(place, key, value)
    db.commit()
    db.refresh(place)
    return place


def delete_place(db: Session, place_id: str) -> None:
    places.remove(db, get_place(db, place_id))
    db.commit()

