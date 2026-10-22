from contextlib import asynccontextmanager

from fastapi import Depends, FastAPI, HTTPException, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import FileResponse, JSONResponse

from app.api.routers import detect, streams
from app.core.auth import view_evidence
from app.inference.detector import SittingDetector
from app.infrastructure.evidence_store import evidence_file
from app.infrastructure.redis_publisher import redis_client
from app.streaming.manager import StreamManager
from app.workers.clip_worker import ClipWorker


@asynccontextmanager
async def lifespan(app: FastAPI):
    app.state.detector = SittingDetector()
    app.state.streams = StreamManager(app.state.detector)
    app.state.clip_worker = ClipWorker(app.state.streams)
    app.state.clip_worker.start()
    yield
    app.state.clip_worker.stop()
    app.state.streams.stop_all()
    redis_client.close()


app = FastAPI(title="LV-Vision Detection Sitting", lifespan=lifespan)
app.include_router(detect.router)
app.include_router(streams.router)


@app.exception_handler(HTTPException)
async def http_error(_: Request, exc: HTTPException) -> JSONResponse:
    code = {401: "unauthorized", 403: "forbidden", 404: "not_found", 409: "conflict",
            413: "image_too_large", 422: "validation_error", 503: "dependency_unavailable"}.get(exc.status_code, "bad_request")
    return JSONResponse(status_code=exc.status_code, content={"code": code, "message": str(exc.detail)})


@app.exception_handler(RequestValidationError)
async def validation_error(_: Request, exc: RequestValidationError) -> JSONResponse:
    first = exc.errors()[0]
    return JSONResponse(status_code=422,
                        content={"code": "validation_error", "message": f"Dữ liệu không hợp lệ: {first['msg']}"})


@app.get("/health")
def health() -> dict:
    redis_client.ping()
    return {"status": "OK", "service": "detection-sitting", "model": "yolo11n-pose"}


@app.get("/evidence/{relative_path:path}", dependencies=[Depends(view_evidence)])
def evidence(relative_path: str) -> FileResponse:
    try:
        path = evidence_file(relative_path)
    except FileNotFoundError as exc:
        raise HTTPException(404, "Không tìm thấy minh chứng") from exc
    media_type = "video/mp4" if path.suffix.lower() == ".mp4" else "image/jpeg"
    return FileResponse(path, media_type=media_type)
