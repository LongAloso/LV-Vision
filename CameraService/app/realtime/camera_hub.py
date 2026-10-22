import asyncio
import time

from fastapi import HTTPException, WebSocket, WebSocketDisconnect
from sqlalchemy import select
from sqlalchemy.orm import Session

from app.core.auth import authorize_token
from app.infrastructure.db import Camera, engine
from app.schemas import CameraStatus


def status_payload(camera: Camera) -> dict:
    return CameraStatus(
        camera_id=camera.id, is_active=camera.is_active, fps=camera.fps,
        target_fps=camera.target_fps, p50_ms=camera.p50_ms, p95_ms=camera.p95_ms,
        worker_id=camera.worker_id, status_updated_at=camera.status_updated_at,
        reference_frame_url=camera.reference_frame_url,
    ).model_dump(mode="json", by_alias=True)


class CameraHub:
    def __init__(self) -> None:
        self.connections: dict[WebSocket, asyncio.Lock] = {}

    async def send(self, socket: WebSocket, payload: dict) -> None:
        async with self.connections[socket]:
            await socket.send_json(payload)

    async def broadcast(self, payload: dict) -> None:
        for socket in list(self.connections):
            try:
                await self.send(socket, payload)
            except (RuntimeError, WebSocketDisconnect):
                self.connections.pop(socket, None)


hub = CameraHub()


async def camera_socket(socket: WebSocket) -> None:
    await socket.accept()
    try:
        first = await asyncio.wait_for(socket.receive_json(), timeout=10)
        if first.get("type") != "auth" or not isinstance(first.get("token"), str):
            await socket.close(code=1008, reason="Cần gửi auth trước")
            return
        token = first["token"]
        claims, _ = await authorize_token(token, "Camera.Read")
        hub.connections[socket] = asyncio.Lock()
        with Session(engine) as session:
            rows = session.scalars(select(Camera).order_by(Camera.id)).all()
            snapshot = [status_payload(camera) for camera in rows]
        await hub.send(socket, {"type": "CameraSnapshot", "items": snapshot})
        while True:
            remaining = int(claims["exp"] - time.time())
            if remaining <= 0:
                await socket.close(code=1008, reason="JWT hết hạn")
                return
            try:
                message = await asyncio.wait_for(socket.receive_json(), timeout=min(remaining, 30))
                if message.get("type") == "ping":
                    await hub.send(socket, {"type": "pong"})
            except asyncio.TimeoutError:
                await authorize_token(token, "Camera.Read")
    except (HTTPException, ValueError, TypeError):
        await socket.close(code=1008, reason="Không được truy cập camera")
    except WebSocketDisconnect:
        pass
    finally:
        hub.connections.pop(socket, None)

