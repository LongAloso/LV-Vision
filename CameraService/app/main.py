from contextlib import asynccontextmanager

from fastapi import FastAPI, HTTPException, Request, WebSocket
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from sqlalchemy import text

from app.api.routers import cameras, places, scenarios
from app.infrastructure.db import engine, initialize
from app.realtime.camera_hub import camera_socket


@asynccontextmanager
async def lifespan(_: FastAPI):
    initialize()
    yield
    engine.dispose()


app = FastAPI(title="LV-Vision Camera Service", lifespan=lifespan)
app.include_router(cameras.router)
app.include_router(places.router)
app.include_router(scenarios.router)


@app.exception_handler(HTTPException)
async def http_error(_: Request, exc: HTTPException) -> JSONResponse:
    code = {401: "unauthorized", 403: "forbidden", 404: "not_found", 409: "conflict", 503: "dependency_unavailable"}.get(exc.status_code, "bad_request")
    return JSONResponse(status_code=exc.status_code, content={"code": code, "message": str(exc.detail)})


@app.exception_handler(RequestValidationError)
async def validation_error(_: Request, exc: RequestValidationError) -> JSONResponse:
    return JSONResponse(status_code=422, content={"code": "validation_error", "message": str(exc.errors()[0]["msg"])})


@app.get("/health")
def health() -> dict:
    with engine.connect() as connection:
        connection.execute(text("SELECT 1"))
    return {"status": "OK", "service": "camera-service"}


@app.websocket("/ws/cameras")
async def websocket_cameras(socket: WebSocket) -> None:
    await camera_socket(socket)
