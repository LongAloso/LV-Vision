"""Live inference + Redis event smoke test through Gateway.

Requires Docker Compose Phase 2B running. Uses a generated test image; this is
not an accuracy benchmark for CCTV footage. No JWT or internal key is printed.
"""

import json
import os
import subprocess
import urllib.error
import urllib.request
from pathlib import Path
from uuid import uuid4

from redis_fixture import clean_fixture_streams


ROOT = Path(__file__).resolve().parents[2]
FIXTURE = Path(__file__).resolve().parent / "fixtures" / "seated-worker.png"
BASE = os.getenv("LV_GATEWAY_URL", "http://localhost:8888").rstrip("/")


def request(method: str, path: str, body: bytes | None = None, headers: dict | None = None):
    operation = urllib.request.Request(BASE + path, data=body, method=method, headers=headers or {})
    try:
        with urllib.request.urlopen(operation, timeout=90) as response:
            return response.status, response.headers, response.read()
    except urllib.error.HTTPError as response:
        return response.code, response.headers, response.read()


def multipart(image: bytes, fields: dict[str, str]) -> tuple[bytes, str]:
    boundary = f"lvvision-{uuid4().hex}"
    chunks = []
    for name, value in fields.items():
        chunks.append(f"--{boundary}\r\nContent-Disposition: form-data; name=\"{name}\"\r\n\r\n{value}\r\n".encode())
    chunks.append(f"--{boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"seated-worker.png\"\r\nContent-Type: image/png\r\n\r\n".encode())
    chunks.extend([image, b"\r\n", f"--{boundary}--\r\n".encode()])
    return b"".join(chunks), f"multipart/form-data; boundary={boundary}"


def redis_length() -> int:
    raw = subprocess.check_output(
        ["docker", "compose", "-f", "docker-compose.dev.yml", "exec", "-T",
         "redis", "redis-cli", "XLEN", "analytics:events"],
        cwd=ROOT, text=True, timeout=15,
    )
    return int(raw.strip())


def latest_event() -> dict:
    raw = subprocess.check_output(
        ["docker", "compose", "-f", "docker-compose.dev.yml", "exec", "-T",
         "redis", "redis-cli", "--raw", "XREVRANGE", "analytics:events", "+", "-", "COUNT", "1"],
        cwd=ROOT, text=True, timeout=15,
    )
    return json.loads(raw.strip().splitlines()[-1])


def verify() -> None:
    status, _, raw = request("GET", "/detect/sitting/health")
    assert status == 200, (status, raw[:200])
    payload = json.dumps({"userNameOrEmail": "admin", "password": os.getenv("LV_TEST_ADMIN_PASSWORD", "Admin@123")}).encode()
    status, _, raw = request("POST", "/identityservice/api/auth/login", payload,
                             {"Content-Type": "application/json"})
    assert status == 200, (status, raw[:200])
    login = json.loads(raw)
    token = login.get("accessToken") or login.get("token")
    assert token

    image = FIXTURE.read_bytes()
    body, content_type = multipart(image, {
        "cameraId": "cam-fixture", "zoneId": "zone-fixture",
        "behaviorVersion": "v2",
        "zonePolygon": json.dumps([[0, 0], [10000, 0], [10000, 10000], [0, 10000]]),
    })
    assert request("POST", "/detect/sitting/detect", body, {"Content-Type": content_type})[0] == 401
    status, _, raw = request("POST", "/detect/sitting/detect", body,
                             {"Content-Type": content_type, "Authorization": f"Bearer {token}"})
    assert status == 200, (status, raw[:350])
    detections = json.loads(raw)["detections"]
    seated = [item for item in detections if item["behaviorCode"] == "PERSON_SITTING" and item["inZone"]]
    assert seated, detections
    assert seated[0]["snapshotUrl"] and len(seated[0]["skeleton"]) == 17

    before = redis_length()
    publish_body, publish_content_type = multipart(image, {
        "cameraId": "cam-fixture", "zoneId": "zone-fixture", "publish": "true",
        "behaviorVersion": "v2",
        "zonePolygon": json.dumps([[0, 0], [10000, 0], [10000, 10000], [0, 10000]]),
    })
    key = os.getenv("SPC_INTERNAL_API_KEY", "development-only-internal-api-key")
    status, _, raw = request("POST", "/detect/sitting/detect", publish_body,
                             {"Content-Type": publish_content_type, "X-Internal-Key": key})
    assert status == 200, (status, raw[:350])
    assert redis_length() == before + 1
    event = latest_event()
    assert event["event"] == "lv.behavior.detected" and event["cameraId"] == "cam-fixture"
    assert event["inZone"] and "frameBase64" not in event
    published = json.loads(raw)["detections"]
    image_url = next(item["snapshotUrl"] for item in published if item["behaviorCode"] == "PERSON_SITTING")
    status, headers, evidence = request("GET", image_url, headers={"Authorization": f"Bearer {token}"})
    assert status == 200 and evidence.startswith(b"\xff\xd8") and headers.get_content_type() == "image/jpeg"
    print("Phase 2B PASS: YOLO sitting pose, polygon, annotated JPEG, Gateway auth, Redis event")


if __name__ == "__main__":
    try:
        verify()
    finally:
        clean_fixture_streams()
