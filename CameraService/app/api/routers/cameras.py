from fastapi import APIRouter, Depends, Query
from sqlalchemy.orm import Session

from app.api.dependencies import get_session, require_internal_key, require_manage, require_read
from app.infrastructure.db.models import Camera
from app.realtime.camera_hub import hub, status_payload
from app.schemas import CameraPatch, CameraRead, CameraStatus, CameraStatusUpdate, CameraWrite, Page
from app.services import camera_service


router = APIRouter(prefix="/api/cameras", tags=["cameras"])


@router.get("")
def list_cameras(page: int = Query(1, ge=1), page_size: int = Query(10, alias="pageSize", ge=1, le=100),
                 _: dict = Depends(require_read), db: Session = Depends(get_session)) -> Page:
    return camera_service.list_cameras(db, page, page_size)


@router.post("", status_code=201, response_model=CameraRead)
async def create_camera(body: CameraWrite, _: dict = Depends(require_manage),
                        db: Session = Depends(get_session)) -> Camera:
    camera = camera_service.create_camera(db, body)
    await hub.broadcast({"type": "CameraStatus", "camera": status_payload(camera)})
    return camera


@router.get("/internal/status")
def internal_status(_: None = Depends(require_internal_key), db: Session = Depends(get_session)) -> dict:
    return camera_service.internal_status(db)


@router.get("/{camera_id}", response_model=CameraRead)
def get_camera(camera_id: str, _: dict = Depends(require_read), db: Session = Depends(get_session)) -> Camera:
    return camera_service.get_camera(db, camera_id)


@router.put("/{camera_id}", response_model=CameraRead)
async def update_camera(camera_id: str, body: CameraWrite, _: dict = Depends(require_manage),
                        db: Session = Depends(get_session)) -> Camera:
    camera = camera_service.update_camera(db, camera_id, body)
    await hub.broadcast({"type": "CameraStatus", "camera": status_payload(camera)})
    return camera


@router.patch("/{camera_id}", response_model=CameraRead)
async def patch_camera(camera_id: str, body: CameraPatch, _: dict = Depends(require_manage),
                       db: Session = Depends(get_session)) -> Camera:
    camera = camera_service.patch_camera(db, camera_id, body)
    await hub.broadcast({"type": "CameraStatus", "camera": status_payload(camera)})
    return camera


@router.delete("/{camera_id}", status_code=204)
async def delete_camera(camera_id: str, _: dict = Depends(require_manage),
                        db: Session = Depends(get_session)) -> None:
    camera_service.delete_camera(db, camera_id)
    await hub.broadcast({"type": "CameraDeleted", "cameraId": camera_id})


@router.get("/{camera_id}/status", response_model=CameraStatus)
def get_camera_status(camera_id: str, _: dict = Depends(require_read), db: Session = Depends(get_session)) -> dict:
    return status_payload(camera_service.get_camera(db, camera_id))


@router.get("/{camera_id}/runtime")
def get_camera_runtime(camera_id: str, _: None = Depends(require_internal_key),
                       db: Session = Depends(get_session)) -> dict:
    return camera_service.runtime(db, camera_id)


@router.put("/{camera_id}/status", response_model=CameraStatus)
async def update_camera_status(camera_id: str, body: CameraStatusUpdate,
                               _: None = Depends(require_internal_key),
                               db: Session = Depends(get_session)) -> dict:
    payload = status_payload(camera_service.update_status(db, camera_id, body))
    await hub.broadcast({"type": "CameraStatus", "camera": payload})
    return payload


@router.post("/{camera_id}/reset-reference", response_model=CameraStatus)
async def reset_reference(camera_id: str, _: dict = Depends(require_manage),
                          db: Session = Depends(get_session)) -> dict:
    payload = status_payload(camera_service.reset_reference(db, camera_id))
    await hub.broadcast({"type": "CameraStatus", "camera": payload})
    return payload

