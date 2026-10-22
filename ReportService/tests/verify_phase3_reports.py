"""Gateway integration for report counts, permissions, date bounds and XLSX."""

import io
import json
import os
import urllib.error
import urllib.request
from urllib.parse import urlencode
import xml.etree.ElementTree as ET
import zipfile
from datetime import date, datetime, time, timedelta, timezone

BASE = os.getenv("LV_GATEWAY_URL", "http://localhost:8888").rstrip("/")


def request(path: str, *, token: str | None = None, body: dict | None = None):
    headers = {"Content-Type": "application/json"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    operation = urllib.request.Request(BASE + path, headers=headers,
                                       data=json.dumps(body).encode() if body is not None else None)
    try:
        with urllib.request.urlopen(operation, timeout=30) as response:
            return response.status, response.headers.get_content_type(), response.read()
    except urllib.error.HTTPError as response:
        return response.code, response.headers.get_content_type(), response.read()


def main() -> None:
    status, _, body = request("/identityservice/api/auth/login", body={
        "userNameOrEmail": os.getenv("LV_TEST_ADMIN_USER", "admin"),
        "password": os.getenv("LV_TEST_ADMIN_PASSWORD", "Admin@123")})
    assert status == 200, (status, body[:250])
    token = json.loads(body).get("accessToken") or json.loads(body)["token"]

    assert request("/reportservice/statistics")[0] == 401
    assert request("/reportservice/statistics", token="invalid")[0] == 401
    assert request("/reportservice/statistics?from=2026-10-16&to=2026-10-15", token=token)[0] == 422
    assert request("/reportservice/statistics?from=2025-01-01&to=2026-10-15", token=token)[0] == 422

    status, mime, body = request("/reportservice/statistics", token=token)
    assert status == 200 and mime == "application/json", (status, body[:250])
    report = json.loads(body)
    for key in ("daily", "bySeverity", "byStatus", "byBehavior", "byCamera"):
        assert sum(item["count"] for item in report[key]) == report["total"], key
    assert len(report["daily"]) == (date.fromisoformat(report["to"]) - date.fromisoformat(report["from"])).days + 1
    assert report["timezone"] == "UTC"

    start = datetime.combine(date.fromisoformat(report["from"]), time.min, timezone.utc)
    end = datetime.combine(date.fromisoformat(report["to"]) + timedelta(days=1), time.min, timezone.utc)
    status, _, body = request("/alertservice/alerts?" + urlencode({
        "from": start.isoformat(), "to": end.isoformat(), "pageSize": 1}), token=token)
    assert status == 200 and json.loads(body)["total"] == report["total"], (status, report["total"], body[:250])

    if report["byCamera"]:
        selected = report["byCamera"][0]
        status, _, body = request("/reportservice/statistics?" + urlencode({"cameraId": selected["id"]}), token=token)
        assert status == 200 and json.loads(body)["total"] >= selected["count"]
    if report["bySeverity"]:
        selected = report["bySeverity"][0]
        status, _, body = request("/reportservice/statistics?" + urlencode({"severity": selected["severity"]}), token=token)
        assert status == 200 and json.loads(body)["total"] >= selected["count"]

    status, mime, body = request("/reportservice/statistics/export", token=token)
    assert status == 200 and mime == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", status
    with zipfile.ZipFile(io.BytesIO(body)) as book:
        sheet = ET.fromstring(book.read("xl/worksheets/sheet1.xml"))
        rows = sheet.findall(".//{http://schemas.openxmlformats.org/spreadsheetml/2006/main}row")
        assert len(rows) == report["total"] + 1, (len(rows), report["total"])

    status, _, body = request("/systemservice/system/health", token=token)
    assert status == 200, (status, body[:250])
    services = {row["name"]: row["status"] for row in json.loads(body)["services"]}
    assert services.get("report-service") == "OK", services
    print(f"Phase 3 report OK: {report['total']} alerts, {len(rows)} Excel rows, UTC/day/auth checks")


if __name__ == "__main__":
    main()
