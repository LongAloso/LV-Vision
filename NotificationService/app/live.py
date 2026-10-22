"""WS push and Redis consumer, with a persisted stream cursor."""

import asyncio
import json
import logging
import os
import threading
import time
from uuid import uuid4

from fastapi import HTTPException, WebSocket, WebSocketDisconnect
from redis import Redis
from sqlalchemy import select
from sqlalchemy.orm import Session

from app.auth import authorize
from app.store import Notification, as_json, engine


LOG = logging.getLogger(__name__)
redis_client = Redis.from_url(os.getenv("REDIS_URL", "redis://redis:6379/0"),
                              decode_responses=True, socket_timeout=5)
CURSOR_KEY = "notification:events:last_id"


class Hub:
    def __init__(self):
        self.clients: dict[WebSocket, asyncio.Lock] = {}
        self.loop: asyncio.AbstractEventLoop | None = None

    async def broadcast(self, payload: dict) -> None:
        for socket, lock in list(self.clients.items()):
            try:
                async with lock:
                    await socket.send_json(payload)
            except (RuntimeError, WebSocketDisconnect, OSError):
                self.clients.pop(socket, None)

    def from_worker(self, payload: dict) -> None:
        if self.loop and self.clients:
            try:
                asyncio.run_coroutine_threadsafe(self.broadcast(payload), self.loop).result(timeout=5)
            except Exception:
                LOG.exception("Notification broadcast failed")


hub = Hub()


async def socket_handler(socket: WebSocket) -> None:
    await socket.accept()
    try:
        first = await asyncio.wait_for(socket.receive_json(), timeout=10)
        if first.get("type") != "auth" or not isinstance(first.get("token"), str):
            await socket.close(code=1008)
            return
        token = first["token"]
        await authorize(token)
        import jwt
        expiry = jwt.decode(token, options={"verify_signature": False})["exp"]
        hub.clients[socket] = asyncio.Lock()
        while True:
            remaining = int(expiry - time.time())
            if remaining <= 0:
                await socket.close(code=1008)
                return
            try:
                message = await asyncio.wait_for(socket.receive_json(), timeout=min(remaining, 30))
                if message.get("type") == "ping":
                    async with hub.clients[socket]:
                        await socket.send_json({"type": "pong"})
            except asyncio.TimeoutError:
                await authorize(token)
    except (HTTPException, ValueError, TypeError, KeyError, WebSocketDisconnect):
        await socket.close(code=1008)
    finally:
        hub.clients.pop(socket, None)


class Consumer:
    def __init__(self):
        self.stop_event = threading.Event()
        self.thread = threading.Thread(target=self.run, name="notification-consumer", daemon=True)

    def start(self):
        self.thread.start()

    def stop(self):
        self.stop_event.set()
        self.thread.join(timeout=4)

    def process(self, payload: dict):
        if payload.get("event") != "lv.alert.created":
            return
        if any(not isinstance(payload.get(key), str) or not payload[key]
               for key in ("alertId", "severity", "title")):
            raise ValueError("Malformed alert notification")
        with Session(engine) as db:
            if db.scalar(select(Notification.id).where(Notification.alert_id == payload["alertId"])):
                return
            row = Notification(id=f"notice-{uuid4().hex[:12]}", alert_id=payload["alertId"],
                               severity=payload["severity"], title=payload["title"])
            db.add(row)
            db.commit()
            db.refresh(row)
            hub.from_worker({"type": "NewNotification", "notification": as_json(row)})

    def run(self):
        while not self.stop_event.is_set():
            try:
                cursor = redis_client.get(CURSOR_KEY) or "0-0"
                for _, messages in redis_client.xread({"notifications:events": cursor}, count=20, block=1000):
                    for message_id, fields in messages:
                        try:
                            self.process(json.loads(fields["payload"]))
                        except (KeyError, ValueError, TypeError):
                            LOG.exception("Invalid notification %s; skipping", message_id)
                        redis_client.set(CURSOR_KEY, message_id)
            except Exception:
                LOG.exception("Notification consumer failed; retrying")
                self.stop_event.wait(2)
