"""Phase 2D integration: RTSP -> detector -> alert + clip -> notification + system.

Requires a test publisher at rtsp://lv-rtsp-test:8554/seated on lv-vision-dev.
Only its own camera, alert, notification and Redis messages are removed.
"""

import asyncio
import json
import os
import re
import subprocess
import time
import urllib.error
import urllib.request
from datetime import datetime, timezone
from pathlib import Path
from uuid import uuid4

from websockets.asyncio.client import connect


ROOT = Path(__file__).resolve().parents[2]
BASE = os.getenv("LV_GATEWAY_URL", "http://localhost:8888").rstrip("/")
WEB = os.getenv("LV_WEB_URL", "http://localhost:3000").rstrip("/")
RTSP = os.getenv("LV_TEST_RTSP_URL", "rtsp://lv-rtsp-test:8554/seated")


def request(method: str, path: str, body: dict | None = None, token: str | None = None,
            *, base: str = BASE) -> tuple[int, dict | bytes | None, str]:
    headers = {"Content-Type": "application/json"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    operation = urllib.request.Request(base + path, method=method, headers=headers,
                                       data=json.dumps(body).encode() if body is not None else None)
    try:
        with urllib.request.urlopen(operation, timeout=30) as response:
            content = response.read()
            mime = response.headers.get_content_type()
            return response.status, json.loads(content) if content and mime == "application/json" else content, mime
    except urllib.error.HTTPError as response:
        content = response.read()
        try:
            data = json.loads(content)
        except ValueError:
            data = content
        return response.code, data, response.headers.get_content_type()


def login(user: str, password: str) -> str:
    status, data, _ = request("POST", "/identityservice/api/auth/login",
                              {"userNameOrEmail": user, "password": password})
    assert status == 200, (status, data)
    return data.get("accessToken") or data["token"]


def redis(*args: str) -> str:
    return subprocess.check_output(["docker", "compose", "-f", "docker-compose.dev.yml", "exec", "-T",
                                    "redis", "redis-cli", "--json", *args], cwd=ROOT,
                                   text=True, encoding="utf-8", timeout=25).strip()


def clean_streams(camera_id: str, alert_ids: list[str]) -> None:
    for stream in ("analytics:events", "vision:clip:jobs", "notifications:events"):
        cursor = "-"
        while True:
            entries = json.loads(redis("XRANGE", stream, cursor, "+", "COUNT", "500"))
            ids = []
            for message_id, values in entries:
                payload = json.loads(dict(zip(values[::2], values[1::2])).get("payload", "{}"))
                if payload.get("cameraId") == camera_id or payload.get("alertId") in alert_ids:
                    ids.append(message_id)
            if ids:
                redis("XDEL", stream, *ids)
            if len(entries) < 500:
                break
            cursor = "(" + entries[-1][0]


def clean_db(camera_id: str) -> None:
    assert re.fullmatch(r"cam-[a-f0-9]+", camera_id)
    sql = ("DELETE FROM notification.read_receipts WHERE notification_id IN "
           f"(SELECT id FROM notification.items WHERE alert_id IN (SELECT id FROM alert.alerts WHERE camera_id='{camera_id}')); "
           f"DELETE FROM notification.items WHERE alert_id IN (SELECT id FROM alert.alerts WHERE camera_id='{camera_id}'); "
           f"DELETE FROM alert.alerts WHERE camera_id='{camera_id}'")
    subprocess.check_output(["docker", "compose", "-f", "docker-compose.dev.yml", "exec", "-T",
                             "postgres", "psql", "-U", os.getenv("POSTGRES_USER", "lvuser"),
                             "-d", os.getenv("POSTGRES_DB", "lvvision"), "-c", sql],
                            cwd=ROOT, text=True, encoding="utf-8", timeout=25)


def all_alert_ids(camera_id: str) -> list[str]:
    assert re.fullmatch(r"cam-[a-f0-9]+", camera_id)
    output = subprocess.check_output([
        "docker", "compose", "-f", "docker-compose.dev.yml", "exec", "-T", "postgres",
        "psql", "-U", os.getenv("POSTGRES_USER", "lvuser"), "-d", os.getenv("POSTGRES_DB", "lvvision"),
        "-t", "-A", "-c", f"SELECT id FROM alert.alerts WHERE camera_id='{camera_id}'",
    ], cwd=ROOT, text=True, encoding="utf-8", timeout=25)
    return [line.strip() for line in output.splitlines() if line.strip()]


async def next_type(socket, expected: str, timeout: int = 90) -> dict:
    end = time.monotonic() + timeout
    while time.monotonic() < end:
        message = json.loads(await asyncio.wait_for(socket.recv(), timeout=max(1, end - time.monotonic())))
        if message.get("type") == expected:
            return message
    raise TimeoutError(expected)


async def verify() -> None:
    admin = login("admin", os.getenv("LV_TEST_ADMIN_PASSWORD", "Admin@123"))
    mod = login("mod", os.getenv("LV_TEST_MOD_PASSWORD", "Mod@123"))
    assert request("GET", "/notificationservice/notifications")[0] == 401
    assert request("GET", "/systemservice/system/health")[0] == 401
    assert request("POST", "/detect/sitting/streams/start", {"cameraId": "invalid"}, mod)[0] == 403
    status, web, mime = request("GET", "/", base=WEB)
    assert status == 200 and mime == "text/html" and b"LV-Vision" in web
    status, health, _ = request("GET", "/systemservice/system/health", token=admin)
    assert status == 200 and health["resources"] and health["storage"]["retentionDays"] == 30, (status, health)

    place_id = camera_id = scenario_id = None
    alert_ids: list[str] = []
    try:
        status, place, _ = request("POST", "/cameraservice/places",
                                   {"name": f"Phase2D {uuid4().hex[:8]}"}, admin)
        assert status == 201, (status, place)
        place_id = place["id"]
        status, camera, _ = request("POST", "/cameraservice/cameras",
                                    {"name": "Camera 2D RTSP test", "rtspUrl": RTSP,
                                     "placeId": place_id, "targetFps": 5}, admin)
        assert status == 201, (status, camera)
        camera_id = camera["id"]
        status, scenario, _ = request("POST", "/cameraservice/scenarios",
                                      {"cameraId": camera_id, "behaviorCode": "PERSON_SITTING",
                                       "behaviorVersion": "v2", "zoneId": "zone-2d-test",
                                       "zonePolygon": [[0, 0], [10000, 0], [10000, 10000], [0, 10000]]}, admin)
        assert status == 201, (status, scenario)
        scenario_id = scenario["id"]

        async with connect(BASE.replace("http", "ws", 1) + "/ws/alerts") as alert_socket, \
                   connect(BASE.replace("http", "ws", 1) + "/ws/notifications") as notice_socket:
            await alert_socket.send(json.dumps({"type": "auth", "token": admin}))
            await notice_socket.send(json.dumps({"type": "auth", "token": admin}))
            status, started, _ = request("POST", "/detect/sitting/streams/start", {"cameraId": camera_id}, admin)
            assert status == 200 and started["state"] == "started", (status, started)
            pushed = await next_type(alert_socket, "NewAlert")
            received_at = datetime.now(timezone.utc)
            alert = pushed["alert"]
            alert_ids.append(alert["id"])
            assert alert["cameraId"] == camera_id and alert["status"] == "NEW"
            notice = await next_type(notice_socket, "NewNotification")
            assert notice["notification"]["alertId"] == alert["id"], notice
            latency = (received_at - datetime.fromisoformat(alert["detectedAt"])).total_seconds()

            for _ in range(40):
                status, preview, mime = request("GET", f"/detect/sitting/streams/{camera_id}/preview", token=mod)
                if status == 200:
                    break
                await asyncio.sleep(0.5)
            assert status == 200 and mime == "image/jpeg" and preview.startswith(b"\xff\xd8")
            assert request("GET", f"/detect/sitting/streams/{camera_id}/preview")[0] == 401

            for _ in range(50):
                status, detail, _ = request("GET", f"/alertservice/alerts/{alert['id']}", token=admin)
                if detail["clipStatus"] == "READY":
                    break
                await asyncio.sleep(0.5)
            assert detail["clipStatus"] == "READY" and detail["clipUrl"], detail
            status, clip, mime = request("GET", detail["clipUrl"], token=admin)
            assert status == 200 and b"ftyp" in clip[:16]

            status, inbox, _ = request("GET", "/notificationservice/notifications?pageSize=100", token=admin)
            mine = next(item for item in inbox["items"] if item["alertId"] == alert["id"])
            assert not mine["isRead"] and inbox["unreadCount"] > 0
            assert request("PUT", f"/notificationservice/notifications/{mine['id']}/read", token=admin)[0] == 200
            status, mod_inbox, _ = request("GET", "/notificationservice/notifications?pageSize=100", token=mod)
            assert not next(item for item in mod_inbox["items"] if item["id"] == mine["id"])["isRead"]
            status, dashboard, _ = request("GET", "/systemservice/system/health", token=admin)
            assert status == 200 and any(item["id"] == camera_id for item in dashboard["cameras"])
            assert any(item["name"] == "notification-service" and item["status"] == "OK"
                       for item in dashboard["services"])
            assert dashboard["storage"]["usedBytes"] > 0
            print(f"Phase 2D PASS: RTSP preview, alert, MP4, notification WS/inbox, system dashboard, web HTTP; event-to-alert {latency:.2f}s")
    finally:
        if camera_id:
            request("POST", "/detect/sitting/streams/stop", {"cameraId": camera_id}, admin)
            alert_ids = all_alert_ids(camera_id)
        if scenario_id:
            request("DELETE", f"/cameraservice/scenarios/{scenario_id}", token=admin)
        if camera_id:
            request("DELETE", f"/cameraservice/cameras/{camera_id}", token=admin)
        if place_id:
            request("DELETE", f"/cameraservice/places/{place_id}", token=admin)
        if camera_id:
            clean_db(camera_id)
            clean_streams(camera_id, alert_ids)


if __name__ == "__main__":
    asyncio.run(verify())
