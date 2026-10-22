"""Behavior catalog with immutable parameter versions and visible history."""

from uuid import uuid4

from fastapi import APIRouter, Depends, HTTPException, Query
from sqlalchemy import select
from sqlalchemy.exc import IntegrityError
from sqlalchemy.orm import Session

from app.auth import require_behavior_manage, require_behavior_read
from app.schemas import BehaviorRead, BehaviorWrite
from app.store import Behavior, get_session


router = APIRouter(prefix="/api/behaviors", tags=["behaviors"])


def as_json(row: Behavior) -> dict:
    return BehaviorRead.model_validate(row).model_dump(mode="json", by_alias=True)


def existing(db: Session, behavior_id: str) -> Behavior:
    row = db.get(Behavior, behavior_id)
    if row is None:
        raise HTTPException(404, "Không tìm thấy hành vi")
    return row


def versions(db: Session, code: str) -> list[Behavior]:
    rows = db.scalars(select(Behavior).where(Behavior.code == code)).all()
    return sorted(rows, key=lambda row: int(row.version[1:]))


def create_version(db: Session, code: str, body: BehaviorWrite, *, is_system: bool,
                   replace_active: bool) -> Behavior:
    history = versions(db, code)
    number = int(history[-1].version[1:]) + 1 if history else 1
    if replace_active:
        for row in history:
            row.is_active = False
    row = Behavior(id=f"beh-{uuid4().hex[:12]}", version=f"v{number}", is_system=is_system,
                   **body.model_dump())
    db.add(row)
    try:
        db.commit()
    except IntegrityError as exc:
        db.rollback()
        raise HTTPException(409, "Phiên bản hành vi vừa được tạo bởi yêu cầu khác") from exc
    db.refresh(row)
    return row


@router.get("")
def list_behaviors(page: int = Query(1, ge=1), page_size: int = Query(10, alias="pageSize", ge=1, le=100),
                   q: str | None = None, is_active: bool | None = Query(None, alias="isActive"),
                   _: dict = Depends(require_behavior_read), db: Session = Depends(get_session)) -> dict:
    all_rows = db.scalars(select(Behavior)).all()
    latest = {}
    for row in all_rows:
        if row.code not in latest or int(row.version[1:]) > int(latest[row.code].version[1:]):
            latest[row.code] = row
    rows = sorted(latest.values(), key=lambda row: row.code)
    if q:
        needle = q.casefold()
        rows = [row for row in rows if needle in row.code.casefold()
                or needle in row.name_vi.casefold() or needle in row.name_en.casefold()]
    if is_active is not None:
        rows = [row for row in rows if row.is_active == is_active]
    total = len(rows)
    rows = rows[(page - 1) * page_size:page * page_size]
    return {"items": [as_json(row) for row in rows], "page": page, "pageSize": page_size, "total": total}


@router.post("", status_code=201)
def create_behavior(body: BehaviorWrite, _: dict = Depends(require_behavior_manage),
                    db: Session = Depends(get_session)) -> dict:
    if versions(db, body.code):
        raise HTTPException(409, "Mã hành vi đã tồn tại; dùng Sửa hoặc Nhân bản để tạo phiên bản mới")
    return as_json(create_version(db, body.code, body, is_system=False, replace_active=False))


@router.get("/{behavior_id}")
def get_behavior(behavior_id: str, _: dict = Depends(require_behavior_read),
                 db: Session = Depends(get_session)) -> dict:
    return as_json(existing(db, behavior_id))


@router.put("/{behavior_id}", status_code=201)
def update_behavior(behavior_id: str, body: BehaviorWrite,
                    _: dict = Depends(require_behavior_manage), db: Session = Depends(get_session)) -> dict:
    old = existing(db, behavior_id)
    if body.code != old.code:
        raise HTTPException(422, "Không đổi mã hành vi khi tạo phiên bản mới")
    return as_json(create_version(db, old.code, body, is_system=old.is_system, replace_active=True))


@router.delete("/{behavior_id}", status_code=204)
def deactivate_behavior(behavior_id: str, _: dict = Depends(require_behavior_manage),
                        db: Session = Depends(get_session)) -> None:
    row = existing(db, behavior_id)
    row.is_active = False
    db.commit()


@router.post("/{behavior_id}/clone", status_code=201)
def clone_behavior(behavior_id: str, _: dict = Depends(require_behavior_manage),
                   db: Session = Depends(get_session)) -> dict:
    old = existing(db, behavior_id)
    body = BehaviorWrite.model_validate(old)
    return as_json(create_version(db, old.code, body, is_system=old.is_system, replace_active=True))


@router.get("/{behavior_id}/history")
def behavior_history(behavior_id: str, _: dict = Depends(require_behavior_read),
                     db: Session = Depends(get_session)) -> dict:
    row = existing(db, behavior_id)
    rows = versions(db, row.code)
    return {"items": [as_json(item) for item in reversed(rows)], "total": len(rows)}
