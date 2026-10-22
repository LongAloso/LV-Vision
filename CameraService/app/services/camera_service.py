from uuid import uuid4

from fastapi import HTTPException
from sqlalchemy.orm import Session

from app.core.security import decrypt_rtsp, encrypt_rtsp
from app.infrastructure.db.models import Camera, now_utc
from app.infrastructure.db.repositories import cameras, places
from app.schemas import CameraPatch, CameraRead, CameraStatusUpdate, CameraWrite, Page


def get_camera(db: Session, camera_id: str) -> Camera:
    camera = cameras.get(db, camera_id)
    if camera is None:
        raise HTTPException(404, f"Không tìm thấy camera {camera_id}")
    return camera


def _check_place(db: Session, place_id: str | None) -> None:
    if place_id is not None and places.get(db, place_id) is None:
        raise HTTPException(404, f"Không tìm thấy địa điểm {place_id}")


def list_cameras(db: Session, page: int, page_size: int) -> Page:
    total, rows = cameras.page(db, (page - 1) * page_size, page_size)
    return Page(items=[CameraRead.model_validate(row).model_dump(mode="json", by_alias=True) for row in rows],
                page=page, page_size=page_size, total=total)


def create_camera(db: Session, body: CameraWrite) -> Camera:
    _check_place(db, body.place_id)
    camera = Camera(id=f"cam-{uuid4().hex[:10]}",
                    **dict(body.model_dump(), rtsp_url=encrypt_rtsp(body.rtsp_url)))
    cameras.add(db, camera)
    db.commit()
    db.refresh(camera)
    return camera


def update_camera(db: Session, camera_id: str, body: CameraWrite) -> Camera:
    camera = get_camera(db, camera_id)
    _check_place(db, body.place_id)
    for key, value in body.model_dump().items():
        setattr(camera, key, encrypt_rtsp(value) if key == "rtsp_url" else value)
    db.commit()
    db.refresh(camera)
    return camera


def patch_camera(db: Session, camera_id: str, body: CameraPatch) -> Camera:
    camera = get_camera(db, camera_id)
    changes = body.model_dump(exclude_unset=True)
    if not changes:
        raise HTTPException(422, "Cần ít nhất một trường để cập nhật")
    if "place_id" in changes:
        _check_place(db, changes["place_id"])
    for key, value in changes.items():
        if key != "place_id" and value is None:
            raise HTTPException(422, f"{key} không được rỗng")
        setattr(camera, key, encrypt_rtsp(value) if key == "rtsp_url" else value)
    db.commit()
    db.refresh(camera)
    return camera


def delete_camera(db: Session, camera_id: str) -> None:
    cameras.remove(db, get_camera(db, camera_id))
    db.commit()


def internal_status(db: Session) -> dict:
    return {"items": [dict(CameraRead.model_validate(row).model_dump(mode="json", by_alias=True),
                           placeName=row.place.name if row.place else None)
                      for row in cameras.all_by_name(db)]}


def runtime(db: Session, camera_id: str) -> dict:
    camera = get_camera(db, camera_id)
    active = cameras.enabled_scenarios(db, camera_id)
    return {
        "cameraId": camera.id, "cameraName": camera.name,
        "placeId": camera.place_id, "placeName": camera.place.name if camera.place else None,
        "rtspUrl": decrypt_rtsp(camera.rtsp_url), "targetFps": camera.target_fps,
        "scenarios": [{
            "id": scenario.id, "behaviorCode": scenario.behavior_code,
            "behaviorVersion": scenario.behavior_version, "zoneId": scenario.zone_id,
            "zonePolygon": scenario.zone_polygon, "zoneMode": scenario.zone_mode,
            "detectorUrl": scenario.detector_url,
        } for scenario in active],
    }


def update_status(db: Session, camera_id: str, body: CameraStatusUpdate) -> Camera:
    camera = get_camera(db, camera_id)
    for key, value in body.model_dump(exclude_unset=True).items():
        setattr(camera, key, value)
    camera.status_updated_at = now_utc()
    db.commit()
    db.refresh(camera)
    return camera


def reset_reference(db: Session, camera_id: str) -> Camera:
    camera = get_camera(db, camera_id)
    camera.reference_frame_url = None
    camera.status_updated_at = now_utc()
    db.commit()
    db.refresh(camera)
    return camera

