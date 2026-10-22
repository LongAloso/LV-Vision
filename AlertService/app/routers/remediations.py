"""Remediation notes belong to an alert and remain in its detail history."""

from uuid import uuid4

from fastapi import APIRouter, Depends, HTTPException, Query
from sqlalchemy import func, select
from sqlalchemy.orm import Session

from app.auth import require_alert_manage, require_alert_read
from app.schemas import RemediationRead, RemediationWrite
from app.store import Alert, Remediation, get_session
from app.ws import hub


router = APIRouter(prefix="/api/alerts", tags=["remediations"])
list_router = APIRouter(prefix="/api/remediations", tags=["remediations"])


@list_router.get("")
def list_remediations(page: int = Query(1, ge=1), page_size: int = Query(10, alias="pageSize", ge=1, le=100),
                      _: dict = Depends(require_alert_read), db: Session = Depends(get_session)) -> dict:
    total = db.scalar(select(func.count()).select_from(Remediation)) or 0
    rows = db.execute(select(Remediation, Alert).join(Alert, Alert.id == Remediation.alert_id)
                      .order_by(Remediation.created_at.desc(), Remediation.id.desc())
                      .offset((page - 1) * page_size).limit(page_size)).all()
    return {"items": [dict(RemediationRead.model_validate(note).model_dump(mode="json", by_alias=True),
                           alertTitle=alert.title, cameraName=alert.camera_name, status=alert.status)
                      for note, alert in rows], "page": page, "pageSize": page_size, "total": total}


@router.post("/{alert_id}/remediation", status_code=201)
async def add_remediation(alert_id: str, body: RemediationWrite,
                          _: dict = Depends(require_alert_manage),
                          db: Session = Depends(get_session)) -> dict:
    alert = db.get(Alert, alert_id)
    if alert is None:
        raise HTTPException(404, "Không tìm thấy cảnh báo")
    row = Remediation(id=f"rem-{uuid4().hex[:12]}", alert_id=alert_id, **body.model_dump())
    db.add(row)
    db.commit()
    db.refresh(row)
    payload = RemediationRead.model_validate(row).model_dump(mode="json", by_alias=True)
    await hub.broadcast({"type": "AlertUpdate", "alertId": alert_id, "remediation": payload})
    return payload
