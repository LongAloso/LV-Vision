"""Remove only Phase 2B fixture records from shared development Redis Streams."""

import json
import subprocess
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]


def redis(*args: str) -> str:
    return subprocess.check_output(
        ["docker", "compose", "-f", "docker-compose.dev.yml", "exec", "-T",
         "redis", "redis-cli", "--json", *args], cwd=ROOT, text=True, timeout=20).strip()


def clean_fixture_streams() -> dict[str, int]:
    removed: dict[str, int] = {}
    for stream in ("analytics:events", "vision:clip:jobs"):
        cursor = "-"
        total = 0
        while True:
            entries = json.loads(redis("XRANGE", stream, cursor, "+", "COUNT", "500"))
            ids = []
            for message_id, raw_fields in entries:
                fields = dict(zip(raw_fields[::2], raw_fields[1::2]))
                payload = json.loads(fields.get("payload", "{}"))
                fixture = (payload.get("cameraId") == "cam-fixture"
                           or payload.get("zoneId") == "zone-rtsp-test") if stream == "analytics:events" else (
                               str(payload.get("alertId", "")).startswith("alert-test-"))
                if fixture:
                    ids.append(message_id)
            if ids:
                total += int(json.loads(redis("XDEL", stream, *ids)))
            if len(entries) < 500:
                break
            cursor = "(" + entries[-1][0]
        removed[stream] = total
    return removed


if __name__ == "__main__":
    print(clean_fixture_streams())
