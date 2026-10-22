"""Search, inspect and resolve detected behavior alerts."""

from datetime import datetime

from fastapi import APIRouter, Depends, HTTPException, Query
from sqlalchemy import func, or_, select
from sqlalchemy.orm import Session

from app.auth import require_alert_manage, require_alert_read
from app.schemas import AlertDetail, AlertRead, AssignRequest, CloseRequest
from app.store import Alert, get_session
from app.ws import hub


router = APIRouter(prefix="/api/alerts", tags=["alerts"])


def alert_json(row: Alert, *, detail: bool = False) -> dict:
    schema = AlertDetail if detail else AlertRead
    return schema.model_validate(row).model_dump(mode="json", by_alias=True)


def existing(db: Session, alert_id: str) -> Alert:
    alert = db.get(Alert, alert_id)
    if alert is None:
        raise HTTPException(404, "Không tìm thấy cảnh báo")
    return alert


def open_alert(alert: Alert) -> None:
    if alert.status in {"CLOSED", "FALSE_POSITIVE"}:
        raise HTTPException(409, "Cảnh báo đã kết thúc")


async def save_update(db: Session, alert: Alert) -> dict:
    db.commit()
    db.refresh(alert)
    payload = alert_json(alert)
    await hub.broadcast({"type": "AlertUpdate", "alert": payload})
    return payload


@router.get("")
def list_alerts(status: str | None = None, camera_id: str | None = Query(None, alias="cameraId"),
                place_id: str | None = Query(None, alias="placeId"),
                behavior_code: str | None = Query(None, alias="behaviorCode"),
                severity: str | None = None, from_at: datetime | None = Query(None, alias="from"),
                to_at: datetime | None = Query(None, alias="to"), q: str | None = None,
                page: int = Query(1, ge=1), page_size: int = Query(10, alias="pageSize", ge=1, le=100),
                _: dict = Depends(require_alert_read), db: Session = Depends(get_session)) -> dict:
    if from_at and from_at.tzinfo is None or to_at and to_at.tzinfo is None:
        raise HTTPException(422, "Mốc thời gian cần có múi giờ")
    if from_at and to_at and from_at > to_at:
        raise HTTPException(422, "Khoảng thời gian không hợp lệ")
    filters = []
    if status:
        filters.append(Alert.status == status.upper())
    if camera_id:
        filters.append(Alert.camera_id == camera_id)
    if place_id:
        filters.append(Alert.place_id == place_id)
    if behavior_code:
        filters.append(Alert.behavior_code == behavior_code)
    if severity:
        filters.append(Alert.severity == severity.upper())
    if from_at:
        filters.append(Alert.detected_at >= from_at)
    if to_at:
        filters.append(Alert.detected_at <= to_at)
    if q:
        pattern = f"%{q.strip()}%"
        filters.append(or_(Alert.title.ilike(pattern), Alert.camera_name.ilike(pattern),
                           Alert.place_name.ilike(pattern), Alert.behavior_code.ilike(pattern),
                           Alert.zone_id.ilike(pattern)))
    total = db.scalar(select(func.count()).select_from(Alert).where(*filters)) or 0
    rows = db.scalars(select(Alert).where(*filters).order_by(Alert.detected_at.desc(), Alert.id.desc())
                      .offset((page - 1) * page_size).limit(page_size)).all()
    return {"items": [alert_json(row) for row in rows], "page": page, "pageSize": page_size, "total": total}


@router.get("/{alert_id}")
def get_alert(alert_id: str, _: dict = Depends(require_alert_read),
              db: Session = Depends(get_session)) -> dict:
    return alert_json(existing(db, alert_id), detail=True)


@router.put("/{alert_id}/assign")
async def assign(alert_id: str, body: AssignRequest, _: dict = Depends(require_alert_manage),
                 db: Session = Depends(get_session)) -> dict:
    alert = existing(db, alert_id)
    open_alert(alert)
    alert.assignee = body.assignee.strip()
    alert.status = "ASSIGNED"
    return await save_update(db, alert)


@router.put("/{alert_id}/acknowledge")
async def acknowledge(alert_id: str, _: dict = Depends(require_alert_manage),
                      db: Session = Depends(get_session)) -> dict:
    alert = existing(db, alert_id)
    open_alert(alert)
    alert.status = "ACKNOWLEDGED"
    return await save_update(db, alert)


@router.put("/{alert_id}/close")
async def close(alert_id: str, body: CloseRequest, _: dict = Depends(require_alert_manage),
                db: Session = Depends(get_session)) -> dict:
    alert = existing(db, alert_id)
    open_alert(alert)
    alert.reason = body.reason.strip()
    alert.status = "CLOSED"
    return await save_update(db, alert)


@router.put("/{alert_id}/mark-false")
async def mark_false(alert_id: str, _: dict = Depends(require_alert_manage),
                     db: Session = Depends(get_session)) -> dict:
    alert = existing(db, alert_id)
    open_alert(alert)
    alert.status = "FALSE_POSITIVE"
    alert.reason = "Đánh dấu phát hiện sai"
    return await save_update(db, alert)
