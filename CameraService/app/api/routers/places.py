from fastapi import APIRouter, Depends, Query
from sqlalchemy.orm import Session

from app.api.dependencies import get_session, require_manage, require_read
from app.infrastructure.db.models import Place
from app.schemas import Page, PlaceRead, PlaceWrite
from app.services import place_service


router = APIRouter(prefix="/api/places", tags=["places"])


@router.get("")
def list_places(page: int = Query(1, ge=1), page_size: int = Query(10, alias="pageSize", ge=1, le=100),
                _: dict = Depends(require_read), db: Session = Depends(get_session)) -> Page:
    return place_service.list_places(db, page, page_size)


@router.post("", status_code=201, response_model=PlaceRead)
def create_place(body: PlaceWrite, _: dict = Depends(require_manage), db: Session = Depends(get_session)) -> Place:
    return place_service.create_place(db, body)


@router.get("/{place_id}", response_model=PlaceRead)
def get_place(place_id: str, _: dict = Depends(require_read), db: Session = Depends(get_session)) -> Place:
    return place_service.get_place(db, place_id)


@router.put("/{place_id}", response_model=PlaceRead)
def update_place(place_id: str, body: PlaceWrite, _: dict = Depends(require_manage),
                 db: Session = Depends(get_session)) -> Place:
    return place_service.update_place(db, place_id, body)


@router.delete("/{place_id}", status_code=204)
def delete_place(place_id: str, _: dict = Depends(require_manage), db: Session = Depends(get_session)) -> None:
    place_service.delete_place(db, place_id)

