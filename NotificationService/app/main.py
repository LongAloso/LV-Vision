"""User inbox and live bell for alerts."""

import asyncio
from contextlib import asynccontextmanager

from fastapi import Depends, FastAPI, HTTPException, Query, WebSocket
from sqlalchemy import func, select, text
from sqlalchemy.orm import Session

from app.auth import current_user
from app.live import Consumer, hub, redis_client, socket_handler
from app.store import Notification, ReadReceipt, as_json, engine, get_session
from app.store import initialize


@asynccontextmanager
async def lifespan(app: FastAPI):
    initialize()
    hub.loop = asyncio.get_running_loop()
    app.state.consumer = Consumer()
    app.state.consumer.start()
    yield
    app.state.consumer.stop()
    redis_client.close()
    engine.dispose()


app = FastAPI(title="LV-Vision Notification Service", lifespan=lifespan)


@app.get("/health")
def health() -> dict:
    with engine.connect() as connection:
        connection.execute(text("SELECT 1"))
    redis_client.ping()
    if not app.state.consumer.thread.is_alive():
        raise HTTPException(503, "Notification consumer chưa sẵn sàng")
    return {"status": "OK", "service": "notification-service"}


@app.get("/api/notifications")
def list_notifications(page: int = Query(1, ge=1), page_size: int = Query(10, alias="pageSize", ge=1, le=100),
                       unread_only: bool = Query(False, alias="unreadOnly"),
                       user: dict = Depends(current_user), db: Session = Depends(get_session)) -> dict:
    user_id = user["userId"]
    query = select(Notification, ReadReceipt).outerjoin(
        ReadReceipt, (ReadReceipt.notification_id == Notification.id) & (ReadReceipt.user_id == user_id))
    if unread_only:
        query = query.where(ReadReceipt.notification_id.is_(None))
    rows = db.execute(query.order_by(Notification.created_at.desc(), Notification.id.desc())
                      .offset((page - 1) * page_size).limit(page_size)).all()
    total = db.scalar(select(func.count()).select_from(query.subquery())) or 0
    unread = db.scalar(select(func.count()).select_from(Notification).outerjoin(
        ReadReceipt, (ReadReceipt.notification_id == Notification.id) & (ReadReceipt.user_id == user_id))
                       .where(ReadReceipt.notification_id.is_(None))) or 0
    return {"items": [as_json(row, receipt is not None) for row, receipt in rows],
            "page": page, "pageSize": page_size, "total": total, "unreadCount": unread}


@app.put("/api/notifications/{notification_id}/read")
def mark_read(notification_id: str, user: dict = Depends(current_user),
              db: Session = Depends(get_session)) -> dict:
    row = db.get(Notification, notification_id)
    if row is None:
        raise HTTPException(404, "Không tìm thấy thông báo")
    key = (notification_id, user["userId"])
    if db.get(ReadReceipt, key) is None:
        db.add(ReadReceipt(notification_id=notification_id, user_id=user["userId"]))
        db.commit()
    return as_json(row, True)


@app.websocket("/ws/notifications")
async def notification_socket(socket: WebSocket) -> None:
    await socket_handler(socket)
