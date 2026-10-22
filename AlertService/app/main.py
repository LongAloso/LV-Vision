from contextlib import asynccontextmanager

from fastapi import FastAPI, HTTPException, Request, WebSocket
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from sqlalchemy import text

from app.routers import alerts, behaviors, remediations
from app.store import engine, initialize
from app.workers.clip_result import ClipResultConsumer
from app.workers.consumer import EventConsumer, redis_client
from app.ws import alert_socket, hub


@asynccontextmanager
async def lifespan(app: FastAPI):
    import asyncio
    initialize()
    hub.loop = asyncio.get_running_loop()
    app.state.events = EventConsumer()
    app.state.clips = ClipResultConsumer()
    app.state.events.start()
    app.state.clips.start()
    yield
    app.state.events.stop()
    app.state.clips.stop()
    redis_client.close()
    engine.dispose()


app = FastAPI(title="LV-Vision Alert Service", lifespan=lifespan)
app.include_router(alerts.router)
app.include_router(behaviors.router)
app.include_router(remediations.router)
app.include_router(remediations.list_router)


@app.exception_handler(HTTPException)
async def http_error(_: Request, exc: HTTPException) -> JSONResponse:
    code = {401: "unauthorized", 403: "forbidden", 404: "not_found", 409: "conflict",
            422: "validation_error", 503: "dependency_unavailable"}.get(exc.status_code, "bad_request")
    return JSONResponse(status_code=exc.status_code, content={"code": code, "message": str(exc.detail)})


@app.exception_handler(RequestValidationError)
async def validation_error(_: Request, exc: RequestValidationError) -> JSONResponse:
    return JSONResponse(status_code=422, content={"code": "validation_error",
                                                 "message": f"Dữ liệu không hợp lệ: {exc.errors()[0]['msg']}"})


@app.get("/health")
def health() -> dict:
    with engine.connect() as connection:
        connection.execute(text("SELECT 1"))
    redis_client.ping()
    if not app.state.events.thread.is_alive() or not app.state.clips.thread.is_alive():
        raise HTTPException(503, "Worker cảnh báo chưa sẵn sàng")
    return {"status": "OK", "service": "alert-service"}


@app.websocket("/ws/alerts")
async def websocket_alerts(socket: WebSocket) -> None:
    await alert_socket(socket)
