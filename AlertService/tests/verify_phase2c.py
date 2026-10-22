"""Gateway integration test: detector event -> alert DB -> live WS -> actions.

Uses development seed accounts. Temporary rows and stream messages are removed
after assertions; it does not delete any non-test records.
"""

import asyncio
import json
import os
import re
import subprocess
import urllib.error
import urllib.request
from datetime import datetime, timezone
from pathlib import Path
from uuid import uuid4

from websockets.asyncio.client import connect


ROOT = Path(__file__).resolve().parents[2]
FIXTURE = ROOT / "DetectionSittingService" / "tests" / "fixtures" / "seated-worker.png"
BASE = os.getenv("LV_GATEWAY_URL", "http://localhost:8888").rstrip("/")
KEY = os.getenv("SPC_INTERNAL_API_KEY", "development-only-internal-api-key")


def call(method: str, path: str, body: dict | bytes | None = None, *, token: str | None = None,
         internal: bool = False, content_type: str = "application/json") -> tuple[int, dict | bytes | None]:
    headers = {"Content-Type": content_type}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    if internal:
        headers["X-Internal-Key"] = KEY
    raw_body = json.dumps(body).encode() if isinstance(body, dict) else body
    operation = urllib.request.Request(BASE + path, data=raw_body, headers=headers, method=method)
    try:
        with urllib.request.urlopen(operation, timeout=30) as response:
            raw = response.read()
            return response.status, json.loads(raw) if raw and response.headers.get_content_type() == "application/json" else raw
    except urllib.error.HTTPError as response:
        raw = response.read()
        return response.code, json.loads(raw) if raw else None


def login(username: str, password: str) -> str:
    status, data = call("POST", "/identityservice/api/auth/login",
                        {"userNameOrEmail": username, "password": password})
    assert status == 200, (status, data)
    return data.get("accessToken") or data["token"]


def multipart(image: bytes, fields: dict[str, str]) -> tuple[bytes, str]:
    boundary = f"lv-{uuid4().hex}"
    chunks = []
    for name, value in fields.items():
        chunks.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{name}"\r\n\r\n{value}\r\n'.encode())
    chunks.append(f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="seated.png"\r\nContent-Type: image/png\r\n\r\n'.encode())
    chunks.extend([image, b"\r\n", f"--{boundary}--\r\n".encode()])
    return b"".join(chunks), f"multipart/form-data; boundary={boundary}"


def redis(*args: str) -> str:
    return subprocess.check_output(["docker", "compose", "-f", "docker-compose.dev.yml", "exec", "-T",
                                    "redis", "redis-cli", "--json", *args], cwd=ROOT,
                                   text=True, encoding="utf-8", timeout=20).strip()


def clean_streams(camera_id: str, alert_ids: list[str]) -> None:
    for stream in ("analytics:events", "vision:clip:jobs", "notifications:events"):
        cursor = "-"
        while True:
            entries = json.loads(redis("XRANGE", stream, cursor, "+", "COUNT", "500"))
            ids = []
            for message_id, values in entries:
                fields = dict(zip(values[::2], values[1::2]))
                payload = json.loads(fields.get("payload", "{}"))
                if payload.get("cameraId") == camera_id or payload.get("alertId") in alert_ids:
                    ids.append(message_id)
            if ids:
                redis("XDEL", stream, *ids)
            if len(entries) < 500:
                break
            cursor = "(" + entries[-1][0]


def clean_db(camera_id: str | None, behavior_code: str) -> None:
    assert re.fullmatch(r"cam-[a-f0-9]+", camera_id or "") or camera_id is None
    assert re.fullmatch(r"TEST_[A-F0-9]+", behavior_code)
    statements = []
    if camera_id:
        statements.append(f"DELETE FROM alert.alerts WHERE camera_id = '{camera_id}'")
    statements.append(f"DELETE FROM alert.behaviors WHERE code = '{behavior_code}'")
    subprocess.check_output(["docker", "compose", "-f", "docker-compose.dev.yml", "exec", "-T",
                             "postgres", "psql", "-U", os.getenv("POSTGRES_USER", "lvuser"),
                             "-d", os.getenv("POSTGRES_DB", "lvvision"), "-c", "; ".join(statements)],
                            cwd=ROOT, text=True, encoding="utf-8", timeout=20)


async def next_message(socket, kind: str, timeout: int = 8) -> dict:
    while True:
        message = json.loads(await asyncio.wait_for(socket.recv(), timeout=timeout))
        if message.get("type") == kind:
            return message


async def verify() -> None:
    health = subprocess.check_output([
        "docker", "compose", "-f", "docker-compose.dev.yml", "exec", "-T",
        "alert-service", "python", "-c",
        "import urllib.request; print(urllib.request.urlopen('http://127.0.0.1:5102/health').status)"],
        cwd=ROOT, text=True, timeout=15)
    assert health.strip() == "200", health
    admin = login("admin", os.getenv("LV_TEST_ADMIN_PASSWORD", "Admin@123"))
    moderator = login("mod", os.getenv("LV_TEST_MOD_PASSWORD", "Mod@123"))
    assert call("GET", "/alertservice/alerts")[0] == 401
    assert call("GET", "/alertservice/alerts", token=moderator)[0] == 200

    behavior_code = "TEST_" + uuid4().hex[:8].upper()
    place_id = camera_id = scenario_id = None
    alert_ids: list[str] = []
    try:
        status, catalog = call("GET", "/alertservice/behaviors?pageSize=100", token=admin)
        assert status == 200 and any(item["code"] == "PERSON_SITTING" and item["version"] == "v2"
                                     for item in catalog["items"])
        custom = {"code": behavior_code, "nameVi": "Hành vi thử", "nameEn": "Test behavior",
                  "detectTemplate": "POSTURE_STATE", "severity": "INFO", "zoneType": "Lối đi",
                  "parameters": {"threshold": 1}}
        status, v1 = call("POST", "/alertservice/behaviors", custom, token=admin)
        assert status == 201 and v1["version"] == "v1", (status, v1)
        status, v2 = call("POST", f"/alertservice/behaviors/{v1['id']}/clone", token=admin)
        assert status == 201 and v2["version"] == "v2", (status, v2)
        status, v3 = call("PUT", f"/alertservice/behaviors/{v2['id']}",
                          {**custom, "severity": "WARNING", "parameters": {"threshold": 2}}, token=admin)
        assert status == 201 and v3["version"] == "v3" and v3["severity"] == "WARNING"
        status, history = call("GET", f"/alertservice/behaviors/{v3['id']}/history", token=admin)
        assert status == 200 and history["total"] == 3
        assert call("DELETE", f"/alertservice/behaviors/{v3['id']}", token=moderator)[0] == 403

        status, place = call("POST", "/cameraservice/places", {"name": f"Phase2C {uuid4().hex[:8]}"}, token=admin)
        assert status == 201, (status, place)
        place_id = place["id"]
        status, camera = call("POST", "/cameraservice/cameras",
                              {"name": "Camera 7 test", "rtspUrl": "rtsp://example.invalid/cam7",
                               "placeId": place_id, "targetFps": 5}, token=admin)
        assert status == 201, (status, camera)
        camera_id = camera["id"]
        historical_event = {
            "event": "lv.behavior.detected", "cameraId": camera_id,
            "behaviorCode": behavior_code, "behaviorVersion": "v2",
            "confidence": 0.87, "posture": "SITTING", "bbox": [1, 2, 3, 4],
            "skeleton": [[0, 0]] * 17, "zoneId": "zone-2c-test", "inZone": True,
            "snapshotUrl": "/detect/sitting/evidence/test.jpg",
            "detectedAt": datetime.now(timezone.utc).isoformat(), "metrics": {"template": "POSTURE_STATE"},
        }
        redis("XADD", "analytics:events", "*", "payload", json.dumps(historical_event))
        for _ in range(40):
            _, historical = call("GET", f"/alertservice/alerts?cameraId={camera_id}&behaviorCode={behavior_code}", token=admin)
            if historical["total"] == 1:
                break
            await asyncio.sleep(0.25)
        assert historical["total"] == 1, historical
        assert historical["items"][0]["severity"] == "INFO", historical
        alert_ids.append(historical["items"][0]["id"])
        assert call("DELETE", f"/alertservice/behaviors/{v3['id']}", token=admin)[0] == 204
        redis("XADD", "analytics:events", "*", "payload", json.dumps(historical_event))
        await asyncio.sleep(2)
        _, disabled = call("GET", f"/alertservice/alerts?cameraId={camera_id}&behaviorCode={behavior_code}", token=admin)
        assert disabled["total"] == 1, disabled
        status, scenario = call("POST", "/cameraservice/scenarios",
                                {"cameraId": camera_id, "behaviorCode": "PERSON_SITTING",
                                 "behaviorVersion": "v2", "zoneId": "zone-2c-test",
                                 "zonePolygon": [[0, 0], [10000, 0], [10000, 10000], [0, 10000]]}, token=admin)
        assert status == 201, (status, scenario)
        scenario_id = scenario["id"]

        async with connect(BASE.replace("http", "ws", 1) + "/ws/alerts") as socket:
            await socket.send(json.dumps({"type": "auth", "token": admin}))
            image = FIXTURE.read_bytes()
            body, content_type = multipart(image, {"cameraId": camera_id, "zoneId": "zone-2c-test",
                                                    "behaviorVersion": "v2", "publish": "true",
                                                    "zonePolygon": json.dumps([[0, 0], [10000, 0], [10000, 10000], [0, 10000]])})
            status, detected = call("POST", "/detect/sitting/detect", body, internal=True,
                                    content_type=content_type)
            assert status == 200 and any(item["behaviorCode"] == "PERSON_SITTING"
                                         for item in detected["detections"]), (status, detected)
            pushed = await next_message(socket, "NewAlert")
            alert = pushed["alert"]
            alert_id = alert["id"]
            alert_ids.append(alert_id)
            assert "Camera 7 test" in alert["title"] and alert["placeName"] == place["name"]
            assert alert["status"] == "NEW" and alert["severity"] == "WARNING"
            status, page = call("GET", f"/alertservice/alerts?cameraId={camera_id}&behaviorCode=PERSON_SITTING&status=NEW&pageSize=10", token=moderator)
            assert status == 200 and page["total"] == 1 and page["items"][0]["id"] == alert_id
            status, filtered = call("GET", f"/alertservice/alerts?placeId={place_id}&behaviorCode=PERSON_SITTING&severity=WARNING&q=Camera", token=admin)
            assert status == 200 and any(item["id"] == alert_id for item in filtered["items"])
            status, detail = call("GET", f"/alertservice/alerts/{alert_id}", token=admin)
            assert status == 200 and detail["snapshotUrl"] and len(detail["skeleton"]) == 17
            assert detail["metrics"]["template"] == "POSTURE_STATE"
            assert call("PUT", f"/alertservice/alerts/{alert_id}/assign",
                        {"assignee": "Người trực"}, token=moderator)[0] == 403
            status, assigned = call("PUT", f"/alertservice/alerts/{alert_id}/assign",
                                    {"assignee": "Người trực"}, token=admin)
            assert status == 200 and assigned["status"] == "ASSIGNED"
            assert (await next_message(socket, "AlertUpdate"))["alert"]["status"] == "ASSIGNED"
            status, acknowledged = call("PUT", f"/alertservice/alerts/{alert_id}/acknowledge", token=admin)
            assert status == 200 and acknowledged["status"] == "ACKNOWLEDGED"
            status, note = call("POST", f"/alertservice/alerts/{alert_id}/remediation",
                                {"description": "Nhắc rời lối đi", "person": "Người trực",
                                 "result": "Đã di chuyển"}, token=admin)
            assert status == 201 and note["alertId"] == alert_id
            status, detail = call("GET", f"/alertservice/alerts/{alert_id}", token=admin)
            assert status == 200 and len(detail["remediations"]) == 1
            status, closed = call("PUT", f"/alertservice/alerts/{alert_id}/close",
                                  {"reason": "Đã xử lý"}, token=admin)
            assert status == 200 and closed["status"] == "CLOSED" and closed["reason"] == "Đã xử lý"

            for _ in range(20):
                _, current = call("GET", f"/alertservice/alerts/{alert_id}", token=admin)
                if current["clipStatus"] == "UNAVAILABLE":
                    break
                await asyncio.sleep(0.25)
            assert current["clipStatus"] == "UNAVAILABLE", current

            clip_url = "/detect/sitting/evidence/phase2c-test.mp4"
            redis("XADD", "vision:clip:jobs", "*", "payload", json.dumps({
                "event": "lv.clip.done", "alertId": alert_id, "clipUrl": clip_url}))
            while True:
                ready = await next_message(socket, "ClipReady")
                if ready["alert"]["clipUrl"] == clip_url:
                    break
            assert ready["alert"]["clipStatus"] == "READY"
            latest = call("GET", f"/alertservice/alerts/{alert_id}", token=admin)[1]
            assert latest["clipUrl"] == clip_url and latest["clipStatus"] == "READY"

            status, again = call("POST", "/detect/sitting/detect", body, internal=True,
                                 content_type=content_type)
            assert status == 200 and again["detections"]
            second = await next_message(socket, "NewAlert")
            second_id = second["alert"]["id"]
            alert_ids.append(second_id)
            status, rejected = call("PUT", f"/alertservice/alerts/{second_id}/mark-false", token=admin)
            assert status == 200 and rejected["status"] == "FALSE_POSITIVE"
            while True:
                update = await next_message(socket, "AlertUpdate")
                if update.get("alert", {}).get("id") == second_id:
                    assert update["alert"]["status"] == "FALSE_POSITIVE"
                    break
        print("Phase 2C PASS: catalog versions, live NewAlert/AlertUpdate/ClipReady, filters, assign, remediation, close")
    finally:
        if scenario_id:
            call("DELETE", f"/cameraservice/scenarios/{scenario_id}", token=admin)
        if camera_id:
            call("DELETE", f"/cameraservice/cameras/{camera_id}", token=admin)
        if place_id:
            call("DELETE", f"/cameraservice/places/{place_id}", token=admin)
        clean_db(camera_id, behavior_code)
        if camera_id:
            clean_streams(camera_id, alert_ids)


if __name__ == "__main__":
    asyncio.run(verify())
