"""Read-only health aggregation for the Phase 2 dashboard."""

import asyncio
import os
import shutil
import time
from pathlib import Path

import httpx
import jwt
import psutil
from fastapi import Depends, FastAPI, Header, HTTPException
from redis import Redis


app = FastAPI(title="LV-Vision System Service")
REDIS = Redis.from_url(os.getenv("REDIS_URL", "redis://redis:6379/0"), socket_timeout=3)
SERVICES = {
    "identity": "http://identityservice:5000/health/live",
    "gateway": "http://apigateway:8888/health",
    "camera-service": "http://camera-service:5101/health",
    "detection-sitting": "http://detection-sitting:8001/health",
    "alert-service": "http://alert-service:5102/health",
    "report-service": "http://report-service:5104/health",
    "notification-service": "http://notification-service:5103/health",
    "postgres": None,
    "redis": None,
}
STREAMS = ("analytics:events", "vision:clip:jobs", "notifications:events", "behavior:dryrun:jobs", "plc:commands")
EVIDENCE = Path(os.getenv("EVIDENCE_DIR", "/data/evidence"))


async def authorize(authorization: str | None = Header(None)) -> dict:
    if not authorization or not authorization.startswith("Bearer "):
        raise HTTPException(401, "Thiếu Bearer token")
    token = authorization[7:]
    try:
        claims = jwt.decode(token, os.environ["JWT_KEY"], algorithms=["HS256"],
                            issuer=os.environ["JWT_ISSUER"], audience=os.environ["JWT_AUDIENCE"],
                            options={"require": ["uid", "exp", "nbf"]}, leeway=60)
    except (jwt.PyJWTError, KeyError) as exc:
        raise HTTPException(401, "JWT không hợp lệ") from exc
    try:
        async with httpx.AsyncClient(timeout=5) as client:
            response = await client.get(os.environ["IDENTITY_URL"].rstrip("/") + "/api/auth/me",
                                        headers={"Authorization": f"Bearer {token}"})
    except httpx.HTTPError as exc:
        raise HTTPException(503, "IdentityService chưa sẵn sàng") from exc
    if response.status_code != 200:
        raise HTTPException(401, "Phiên đăng nhập không còn hợp lệ")
    user = response.json()
    if user.get("userId") != claims["uid"] or not user.get("isActive", False):
        raise HTTPException(401, "Người dùng không còn hoạt động")
    if not set(user.get("permissions", [])) & {"Camera.Read", "Camera.Manage"}:
        raise HTTPException(403, "Thiếu quyền Camera.Read")
    return user


async def probe(client: httpx.AsyncClient, name: str, url: str) -> dict:
    started = time.perf_counter()
    try:
        response = await client.get(url)
        healthy = response.status_code == 200
        detail = f"HTTP {response.status_code}"
    except httpx.HTTPError as exc:
        healthy, detail = False, type(exc).__name__
    return {"name": name, "status": "OK" if healthy else "DOWN", "detail": detail,
            "latencyMs": round((time.perf_counter() - started) * 1000, 1)}


def redis_status() -> tuple[dict, list[dict]]:
    started = time.perf_counter()
    try:
        REDIS.ping()
        state = {"name": "redis", "status": "OK", "detail": "PONG",
                 "latencyMs": round((time.perf_counter() - started) * 1000, 1)}
        queues = [{"name": stream, "length": REDIS.xlen(stream) if REDIS.exists(stream) else 0}
                  for stream in STREAMS]
    except Exception as exc:
        state = {"name": "redis", "status": "DOWN", "detail": type(exc).__name__, "latencyMs": None}
        queues = [{"name": stream, "length": None} for stream in STREAMS]
    return state, queues


def storage_status() -> dict:
    usage = shutil.disk_usage(EVIDENCE)
    used = 0
    for root, _, files in os.walk(EVIDENCE):
        for name in files:
            try:
                used += (Path(root) / name).stat().st_size
            except OSError:
                continue
    return {"retentionDays": 30, "usedBytes": used, "freeBytes": usage.free,
            "estimated30DayBytes": None, "cleanupEnabled": False}


@app.get("/health")
def health() -> dict:
    return {"status": "OK", "service": "system-service"}


@app.get("/api/system/health")
async def system_health(_: dict = Depends(authorize)) -> dict:
    async with httpx.AsyncClient(timeout=3) as client:
        service_rows = await asyncio.gather(*(probe(client, name, url) for name, url in SERVICES.items() if url))
        try:
            camera_response = await client.get("http://camera-service:5101/api/cameras/internal/status",
                                               headers={"X-Internal-Key": os.environ["INTERNAL_API_KEY"]})
            camera_response.raise_for_status()
            cameras = camera_response.json()["items"]
        except (httpx.HTTPError, KeyError):
            cameras = []
        try:
            worker_response = await client.get("http://detection-sitting:8001/streams",
                                               headers={"X-Internal-Key": os.environ["INTERNAL_API_KEY"]})
            worker_response.raise_for_status()
            workers = worker_response.json()["items"]
        except (httpx.HTTPError, KeyError):
            workers = []
    redis_row, queues = await asyncio.to_thread(redis_status)
    service_rows.append(redis_row)
    # PostgreSQL is checked through alert-service and camera-service health probes.
    service_rows.append({"name": "postgres", "status": "OK" if all(
        row["status"] == "OK" for row in service_rows if row["name"] in {"alert-service", "camera-service"}
    ) else "DOWN", "detail": "qua camera/alert health", "latencyMs": None})
    memory = psutil.virtual_memory()
    disk = shutil.disk_usage(EVIDENCE)
    return {
        "services": service_rows, "cameras": cameras, "workers": workers,
        "redisQueues": queues, "plcDevices": [],
        "resources": {"scope": "host visible from container", "cpuPercent": psutil.cpu_percent(interval=0.1),
                      "memoryPercent": memory.percent, "diskPercent": round(disk.used / disk.total * 100, 1),
                      "gpu": None},
        "storage": await asyncio.to_thread(storage_status),
    }
