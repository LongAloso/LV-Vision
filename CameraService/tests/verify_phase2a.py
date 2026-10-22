"""Live Phase 2A smoke test against Docker Compose and Gateway :8888.

Run after `docker compose -f docker-compose.dev.yml up -d --build`.
Requires `websockets` on the host (or install CameraService requirements).
Creates temporary dev rows and deletes them in finally. Never prints JWT/RTSP.
"""

import asyncio
import json
import os
import subprocess
import urllib.error
import urllib.request
from uuid import uuid4
from pathlib import Path

from websockets.asyncio.client import connect


BASE = os.getenv("LV_GATEWAY_URL", "http://localhost:8888").rstrip("/")
ROOT = Path(__file__).resolve().parents[2]


def call(method: str, path: str, body: dict | None = None, token: str | None = None,
         internal_key: str | None = None) -> tuple[int, dict | None]:
    headers = {"Content-Type": "application/json"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    if internal_key:
        headers["X-Internal-Key"] = internal_key
    request = urllib.request.Request(
        f"{BASE}{path}",
        data=json.dumps(body).encode() if body is not None else None,
        headers=headers,
        method=method,
    )
    try:
        with urllib.request.urlopen(request, timeout=12) as response:
            raw = response.read()
            return response.status, json.loads(raw) if raw else None
    except urllib.error.HTTPError as response:
        raw = response.read()
        return response.code, json.loads(raw) if raw else None


def login(username: str, password: str) -> str:
    status, data = call("POST", "/identityservice/api/auth/login",
                        {"userNameOrEmail": username, "password": password})
    assert status == 200, (username, status, data)
    token = data.get("accessToken") or data.get("token")
    assert token
    return token


async def verify() -> None:
    admin = login("admin", os.getenv("LV_TEST_ADMIN_PASSWORD", "Admin@123"))
    moderator = login("mod", os.getenv("LV_TEST_MOD_PASSWORD", "Mod@123"))
    assert call("GET", "/cameraservice/cameras")[0] == 401
    assert call("GET", "/cameraservice/cameras", token=moderator)[0] == 200
    assert call("POST", "/cameraservice/cameras", {"name": "denied", "rtspUrl": "rtsp://example/test"}, moderator)[0] == 403

    place_id = camera_id = scenario_id = second_scenario_id = dormant_scenario_id = None
    try:
        status, place = call("POST", "/cameraservice/places",
                             {"name": f"Phase2A test {uuid4().hex[:8]}", "floor": "Tầng 2"}, admin)
        assert status == 201, (status, place)
        place_id = place["id"]

        status, camera = call("POST", "/cameraservice/cameras",
                              {"name": "Camera 7 test", "rtspUrl": "rtsp://example.invalid/cam7",
                               "placeId": place_id, "targetFps": 10}, admin)
        assert status == 201, (status, camera)
        camera_id = camera["id"]
        assert "rtspUrl" not in camera
        status, initial = call("GET", f"/cameraservice/scenarios?cameraId={camera_id}", token=admin)
        assert status == 200 and initial["total"] == 0, "A new camera must not inherit a scenario"
        status, patched = call("PATCH", f"/cameraservice/cameras/{camera_id}",
                               {"name": "Camera 7 renamed", "targetFps": 5}, admin)
        assert status == 200 and patched["name"] == "Camera 7 renamed" and patched["targetFps"] == 5
        assert "rtspUrl" not in patched
        status, detectors = call("GET", "/cameraservice/scenarios/detectors", token=admin)
        assert status == 200 and detectors["items"] == [
            {"behaviorCode": "PERSON_SITTING", "detectorUrl": "http://detection-sitting:8001/detect"}]
        stored_prefix = subprocess.check_output([
            "docker", "compose", "-f", "docker-compose.dev.yml", "exec", "-T", "postgres",
            "psql", "-U", os.getenv("POSTGRES_USER", "lvuser"), "-d", os.getenv("POSTGRES_DB", "lvvision"),
            "-t", "-A", "-c", f"SELECT left(rtsp_url, 7) FROM camera.cameras WHERE id='{camera_id}'",
        ], cwd=ROOT, text=True, encoding="utf-8", timeout=20).strip()
        assert stored_prefix == "enc:v1:", "RTSP URL must be encrypted at rest"

        bad = {"cameraId": camera_id, "behaviorCode": "PERSON_SITTING",
               "zoneId": "zone-aisle-01", "zonePolygon": [[0, 0], [1, 1], [2, 2]]}
        assert call("POST", "/cameraservice/scenarios", bad, admin)[0] == 422
        good = {**bad, "zonePolygon": [[0, 0], [300, 0], [300, 200], [0, 200]],
                "behaviorVersion": "v2"}
        assert call("POST", "/cameraservice/scenarios",
                    {**good, "behaviorCode": "FALL"}, admin)[0] == 422
        assert call("POST", "/cameraservice/scenarios",
                    {**good, "zoneMode": "ALL"}, admin)[0] == 422
        status, dormant = call("POST", "/cameraservice/scenarios",
                               {**good, "behaviorCode": "FALL", "enabled": False,
                                "zoneId": "future-fall-zone"}, admin)
        assert status == 201 and not dormant["enabled"]
        dormant_scenario_id = dormant["id"]
        assert call("PUT", f"/cameraservice/scenarios/{dormant_scenario_id}",
                    {**good, "behaviorCode": "FALL", "zoneId": "future-fall-zone"}, admin)[0] == 422
        status, scenario = call("POST", "/cameraservice/scenarios", good, admin)
        assert status == 201, (status, scenario)
        scenario_id = scenario["id"]
        status, second = call("POST", "/cameraservice/scenarios",
                              {**good, "zoneId": "zone-aisle-02",
                               "zonePolygon": [[310, 0], [600, 0], [600, 200], [310, 200]]}, admin)
        assert status == 201, (status, second)
        second_scenario_id = second["id"]
        status, disabled = call("PUT", f"/cameraservice/scenarios/{scenario_id}",
                                {**good, "enabled": False}, admin)
        assert status == 200 and disabled["enabled"] is False
        status, page = call("GET", f"/cameraservice/cameras?page=1&pageSize=10", token=admin)
        assert status == 200 and any(row["id"] == camera_id for row in page["items"])
        status, page = call("GET", f"/cameraservice/scenarios?cameraId={camera_id}", token=admin)
        assert status == 200 and page["total"] == 3
        assert any(item["zonePolygon"] == good["zonePolygon"] and not item["enabled"] for item in page["items"])
        assert call("GET", f"/cameraservice/cameras/{camera_id}/runtime", token=admin)[0] == 403
        key = os.getenv("SPC_INTERNAL_API_KEY", "development-only-internal-api-key")
        status, runtime = call("GET", f"/cameraservice/cameras/{camera_id}/runtime", internal_key=key)
        assert status == 200 and len(runtime["scenarios"]) == 1
        assert runtime["scenarios"][0]["zoneId"] == "zone-aisle-02"
        assert runtime["rtspUrl"] == "rtsp://example.invalid/cam7"

        async with connect(BASE.replace("http", "ws", 1) + "/ws/cameras") as socket:
            await socket.send(json.dumps({"type": "auth", "token": admin}))
            initial = json.loads(await asyncio.wait_for(socket.recv(), timeout=8))
            assert initial["type"] == "CameraSnapshot"
            assert any(row["cameraId"] == camera_id for row in initial["items"])

            wrong_status, _ = call("PUT", f"/cameraservice/cameras/{camera_id}/status",
                                   {"fps": 8.5, "p50Ms": 44, "p95Ms": 70}, internal_key="wrong")
            assert wrong_status == 403
            status, state = call("PUT", f"/cameraservice/cameras/{camera_id}/status",
                                 {"fps": 8.5, "p50Ms": 44, "p95Ms": 70, "workerId": "worker-2"},
                                 internal_key=key)
            assert status == 200 and state["fps"] == 8.5, (status, state)
            pushed = json.loads(await asyncio.wait_for(socket.recv(), timeout=8))
            assert pushed["type"] == "CameraStatus" and pushed["camera"]["cameraId"] == camera_id
            assert pushed["camera"]["workerId"] == "worker-2"

        assert call("DELETE", f"/cameraservice/cameras/{camera_id}", token=admin)[0] == 204
        assert call("GET", f"/cameraservice/cameras/{camera_id}", token=admin)[0] == 404
        status, remaining = call("GET", f"/cameraservice/scenarios?cameraId={camera_id}", token=admin)
        assert status == 200 and remaining["total"] == 0, "Deleting camera must cascade its zones"
        camera_id = scenario_id = second_scenario_id = dormant_scenario_id = None
        print("Phase 2A PASS: auth, camera CRUD/delete, no default scenario, multiple zones, detector registry, WebSocket")
    finally:
        if scenario_id:
            call("DELETE", f"/cameraservice/scenarios/{scenario_id}", token=admin)
        if second_scenario_id:
            call("DELETE", f"/cameraservice/scenarios/{second_scenario_id}", token=admin)
        if dormant_scenario_id:
            call("DELETE", f"/cameraservice/scenarios/{dormant_scenario_id}", token=admin)
        if camera_id:
            call("DELETE", f"/cameraservice/cameras/{camera_id}", token=admin)
        if place_id:
            call("DELETE", f"/cameraservice/places/{place_id}", token=admin)


if __name__ == "__main__":
    asyncio.run(verify())
