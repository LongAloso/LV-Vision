import asyncio

import httpx
from fastapi import APIRouter, Depends, Header, HTTPException, Request, Response

from app.api.schemas import StreamStart, StreamStop
from app.core.auth import require_access, require_manage, view_evidence


router = APIRouter(prefix="/streams", tags=["streams"])


@router.post("/start")
async def start_stream(body: StreamStart, request: Request,
                       x_internal_key: str | None = Header(default=None),
                       authorization: str | None = Header(default=None)) -> dict:
    await require_manage(authorization, x_internal_key)
    try:
        state = await asyncio.to_thread(request.app.state.streams.start, body.cameraId)
    except httpx.HTTPStatusError as exc:
        raise HTTPException(404 if exc.response.status_code == 404 else 503,
                            "Không lấy được cấu hình camera") from exc
    except httpx.HTTPError as exc:
        raise HTTPException(503, "Camera-service chưa sẵn sàng") from exc
    except ValueError as exc:
        raise HTTPException(422, str(exc)) from exc
    if state == "capacity_reached":
        raise HTTPException(409, "Worker đã đủ hai camera")
    return {"cameraId": body.cameraId, "state": state}


@router.post("/stop")
async def stop_stream(body: StreamStop, request: Request,
                      x_internal_key: str | None = Header(default=None),
                      authorization: str | None = Header(default=None)) -> dict:
    await require_manage(authorization, x_internal_key)
    stopped = await asyncio.to_thread(request.app.state.streams.stop, body.cameraId)
    return {"cameraId": body.cameraId, "state": "stopped" if stopped else "not_running"}


@router.get("")
async def list_streams(request: Request, x_internal_key: str | None = Header(default=None),
                       authorization: str | None = Header(default=None)) -> dict:
    await require_access(authorization, x_internal_key)
    return {"items": request.app.state.streams.statuses()}


@router.get("/{camera_id}/preview")
def latest_preview(camera_id: str, request: Request, overlay: bool = True,
                   _: None = Depends(view_evidence)) -> Response:
    stream = request.app.state.streams.workers.get(camera_id)
    frame = (stream.latest_preview() if overlay else stream.buffer.latest()) if stream else None
    if frame is None:
        raise HTTPException(404, "Camera chưa có khung hình")
    return Response(frame, media_type="image/jpeg", headers={"Cache-Control": "no-store"})

