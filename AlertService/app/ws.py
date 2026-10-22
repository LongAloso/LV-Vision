"""Authenticated live alert channel through the Gateway /ws/alerts route."""

import asyncio
import logging
import time

from fastapi import HTTPException, WebSocket, WebSocketDisconnect

from app.auth import authorize


LOG = logging.getLogger(__name__)


class AlertHub:
    def __init__(self) -> None:
        self.connections: dict[WebSocket, asyncio.Lock] = {}
        self.loop: asyncio.AbstractEventLoop | None = None

    async def broadcast(self, payload: dict) -> None:
        for socket, lock in list(self.connections.items()):
            try:
                async with lock:
                    await socket.send_json(payload)
            except (RuntimeError, WebSocketDisconnect, OSError):
                self.connections.pop(socket, None)

    def broadcast_from_worker(self, payload: dict) -> None:
        if self.loop is None or not self.connections:
            return
        try:
            asyncio.run_coroutine_threadsafe(self.broadcast(payload), self.loop).result(timeout=5)
        except Exception:
            LOG.exception("Cannot broadcast alert update")


hub = AlertHub()


async def alert_socket(socket: WebSocket) -> None:
    await socket.accept()
    try:
        first = await asyncio.wait_for(socket.receive_json(), timeout=10)
        if first.get("type") != "auth" or not isinstance(first.get("token"), str):
            await socket.close(code=1008, reason="Cần gửi auth trước")
            return
        token = first["token"]
        user = await authorize(token, "Alert.Read")
        # Signature was checked by authorize; decode only the expiry for idle rechecks.
        import jwt
        expiry = jwt.decode(token, options={"verify_signature": False})["exp"]
        hub.connections[socket] = asyncio.Lock()
        while True:
            remaining = int(expiry - time.time())
            if remaining <= 0:
                await socket.close(code=1008, reason="JWT hết hạn")
                return
            try:
                message = await asyncio.wait_for(socket.receive_json(), timeout=min(remaining, 30))
                if message.get("type") == "ping":
                    async with hub.connections[socket]:
                        await socket.send_json({"type": "pong"})
            except asyncio.TimeoutError:
                await authorize(token, "Alert.Read")
    except (HTTPException, ValueError, TypeError, KeyError):
        await socket.close(code=1008, reason="Không được truy cập cảnh báo")
    except WebSocketDisconnect:
        pass
    finally:
        hub.connections.pop(socket, None)
