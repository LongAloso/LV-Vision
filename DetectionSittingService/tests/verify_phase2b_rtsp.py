"""Optional live RTSP integration test using an external test stream.

Run a MediaMTX server and publish a looping seated-person video/image to
rtsp://lv-rtsp-test:8554/seated on the lv-vision-dev Docker network first.
Creates and removes its own camera, place and scenario. Never prints secrets.
"""

import json
import os
import subprocess
import time
import urllib.error
import urllib.request
from datetime import datetime, timedelta, timezone
from pathlib import Path
from uuid import uuid4

from redis_fixture import clean_fixture_streams


ROOT = Path(__file__).resolve().parents[2]
BASE = os.getenv("LV_GATEWAY_URL", "http://localhost:8888").rstrip("/")
KEY = os.getenv("SPC_INTERNAL_API_KEY", "development-only-internal-api-key")
RTSP_URL = os.getenv("LV_TEST_RTSP_URL", "rtsp://lv-rtsp-test:8554/seated")


def request(method: str, path: str, body: dict | None = None, *,
            token: str | None = None, internal: bool = False) -> tuple[int, dict | None]:
    headers = {"Content-Type": "application/json"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    if internal:
        headers["X-Internal-Key"] = KEY
    operation = urllib.request.Request(
        BASE + path, data=json.dumps(body).encode() if body is not None else None,
        method=method, headers=headers)
    try:
        with urllib.request.urlopen(operation, timeout=12) as response:
            raw = response.read()
            return response.status, json.loads(raw) if raw else None
    except urllib.error.HTTPError as response:
        raw = response.read()
        return response.code, json.loads(raw) if raw else None


def redis(*args: str) -> str:
    return subprocess.check_output(
        ["docker", "compose", "-f", "docker-compose.dev.yml", "exec", "-T",
         "redis", "redis-cli", "--raw", *args], cwd=ROOT, text=True, timeout=20).strip()


def preview(camera_id: str, token: str, *, overlay: bool) -> bytes:
    path = f"/detect/sitting/streams/{camera_id}/preview?overlay={str(overlay).lower()}"
    operation = urllib.request.Request(BASE + path, headers={"Authorization": f"Bearer {token}"})
    with urllib.request.urlopen(operation, timeout=12) as response:
        assert response.status == 200 and response.headers.get_content_type() == "image/jpeg"
        image = response.read()
        assert image.startswith(b"\xff\xd8")
        return image


def recent_jobs() -> list[dict]:
    lines = redis("XREVRANGE", "vision:clip:jobs", "+", "-", "COUNT", "20").splitlines()
    return [json.loads(line) for line in lines if line.startswith("{")]


def verify() -> None:
    status, login = request("POST", "/identityservice/api/auth/login",
                            {"userNameOrEmail": "admin", "password": os.getenv("LV_TEST_ADMIN_PASSWORD", "Admin@123")})
    assert status == 200, (status, login)
    token = login.get("accessToken") or login.get("token")
    place_id = camera_id = scenario_id = None
    try:
        status, place = request("POST", "/cameraservice/places",
                                {"name": f"RTSP 2B {uuid4().hex[:8]}"}, token=token)
        assert status == 201, (status, place)
        place_id = place["id"]
        status, camera = request("POST", "/cameraservice/cameras",
                                 {"name": "Phase 2B RTSP test", "rtspUrl": RTSP_URL,
                                  "placeId": place_id, "targetFps": 5}, token=token)
        assert status == 201, (status, camera)
        camera_id = camera["id"]
        status, scenario = request("POST", "/cameraservice/scenarios",
                                    {"cameraId": camera_id, "behaviorCode": "PERSON_SITTING",
                                     "behaviorVersion": "v2", "zoneId": "zone-rtsp-test",
                                     "zonePolygon": [[0, 0], [10000, 0], [10000, 10000], [0, 10000]]}, token=token)
        assert status == 201, (status, scenario)
        scenario_id = scenario["id"]
        assert request("POST", "/detect/sitting/streams/start", {"cameraId": camera_id})[0] == 401
        status, started = request("POST", "/detect/sitting/streams/start", {"cameraId": camera_id}, internal=True)
        assert status == 200 and started["state"] == "started", (status, started)

        detected = None
        status_data = None
        deadline = time.monotonic() + 90
        while time.monotonic() < deadline:
            raw = redis("XREVRANGE", "analytics:events", "+", "-", "COUNT", "5")
            for line in raw.splitlines():
                if line.startswith("{"):
                    event = json.loads(line)
                    if event.get("cameraId") == camera_id and event.get("behaviorCode") == "PERSON_SITTING":
                        detected = event
                        break
            _, status_data = request("GET", f"/cameraservice/cameras/{camera_id}/status", token=token)
            if detected and status_data and status_data["fps"] > 0:
                break
            time.sleep(2)
        assert detected, ("No RTSP event", status_data)
        assert status_data and status_data["fps"] > 0, status_data
        assert detected["snapshotUrl"] and len(detected["skeleton"]) == 17
        clean_frame = preview(camera_id, token, overlay=False)
        annotated_frame = preview(camera_id, token, overlay=True)
        assert clean_frame != annotated_frame, "Monitor preview must include pose and zone overlays"

        # The first five-second window includes model warm-up; measure again.
        time.sleep(12)
        _, steady_status = request("GET", f"/cameraservice/cameras/{camera_id}/status", token=token)
        assert steady_status and steady_status["fps"] > 0, steady_status
        assert steady_status["fps"] >= 4.5, ("Below 5 fps development target", steady_status)

        alert_id = f"alert-test-{uuid4().hex[:8]}"
        now = datetime.now(timezone(timedelta(hours=7)))
        job = {"event": "lv.clip.requested", "alertId": alert_id, "cameraId": camera_id,
               "from": (now - timedelta(seconds=3)).isoformat(),
               "to": (now + timedelta(seconds=2)).isoformat()}
        redis("XADD", "vision:clip:jobs", "*", "payload", json.dumps(job))
        completed = None
        deadline = time.monotonic() + 25
        while time.monotonic() < deadline:
            completed = next((item for item in recent_jobs()
                              if item.get("event") == "lv.clip.done" and item.get("alertId") == alert_id), None)
            if completed:
                break
            time.sleep(1)
        assert completed and completed["clipUrl"].endswith(".mp4"), recent_jobs()
        clip_request = urllib.request.Request(BASE + completed["clipUrl"],
                                              headers={"Authorization": f"Bearer {token}"})
        with urllib.request.urlopen(clip_request, timeout=12) as response:
            header = response.read(16)
            assert response.status == 200 and b"ftyp" in header, header
        print(f"Phase 2B RTSP PASS: camera event, warm-up FPS {status_data['fps']}, "
              f"later FPS {steady_status['fps']}, p50 {steady_status['p50Ms']} ms, "
              "clean/annotated preview and MP4 clip")
    finally:
        if camera_id:
            request("POST", "/detect/sitting/streams/stop", {"cameraId": camera_id}, internal=True)
        if scenario_id:
            request("DELETE", f"/cameraservice/scenarios/{scenario_id}", token=token)
        if camera_id:
            request("DELETE", f"/cameraservice/cameras/{camera_id}", token=token)
        if place_id:
            request("DELETE", f"/cameraservice/places/{place_id}", token=token)
        clean_fixture_streams()


if __name__ == "__main__":
    verify()
