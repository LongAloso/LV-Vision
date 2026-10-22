import json

from fastapi import APIRouter, File, Form, Header, HTTPException, Request, UploadFile

from app.api.schemas import parse_polygon
from app.application.detection_pipeline import process_detections
from app.core.auth import require_access
from app.inference.detector import decode_image


router = APIRouter(tags=["detection"])


@router.post("/detect")
async def detect(request: Request, file: UploadFile = File(...),
                 camera_id: str = Form(default="cam-test", alias="cameraId"),
                 zone_id: str = Form(default="zone-test", alias="zoneId"),
                 zone_polygon: str | None = Form(default=None, alias="zonePolygon"),
                 behavior_version: str = Form(default="v1", alias="behaviorVersion"),
                 publish: bool = Form(default=False),
                 authorization: str | None = Header(default=None),
                 x_internal_key: str | None = Header(default=None)) -> dict:
    await require_access(authorization, x_internal_key, publish=publish)
    raw = await file.read(10 * 1024 * 1024 + 1)
    if len(raw) > 10 * 1024 * 1024:
        raise HTTPException(413, "Ảnh vượt quá 10 MB")
    try:
        image = decode_image(raw)
        polygon = parse_polygon(json.loads(zone_polygon)) if zone_polygon else [
            (0.0, 0.0), (float(image.shape[1]), 0.0),
            (float(image.shape[1]), float(image.shape[0])), (0.0, float(image.shape[0]))]
    except (ValueError, TypeError, json.JSONDecodeError) as exc:
        raise HTTPException(422, str(exc)) from exc
    detections = request.app.state.detector.infer(image)
    results = process_detections(image, detections, camera_id=camera_id, zone_id=zone_id,
                                 polygon=polygon, behavior_version=behavior_version, publish=publish)
    return {"detections": [result.model_dump() for result in results]}

