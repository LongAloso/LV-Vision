"""UTC alert statistics and bounded Excel detail export."""

from datetime import date, datetime, time, timedelta, timezone
from io import BytesIO

from fastapi import APIRouter, Depends, HTTPException, Query
from fastapi.responses import StreamingResponse
from openpyxl import Workbook
from sqlalchemy import text
from sqlalchemy.engine import Connection

from app.auth import require_alert_read
from app.store import get_connection

router = APIRouter(prefix="/api/statistics", tags=["statistics"])


def excel_text(value: str | None) -> str:
    """Prevent camera/behavior names from becoming formulas in spreadsheet apps."""
    text_value = value or ""
    if text_value.lstrip().startswith(("=", "+", "-", "@")):
        return "'" + text_value
    return text_value


def period(from_date: date | None, to_date: date | None) -> tuple[datetime, datetime]:
    end_day = to_date or datetime.now(timezone.utc).date()
    start_day = from_date or end_day - timedelta(days=29)
    if start_day > end_day or (end_day - start_day).days > 365:
        raise HTTPException(422, "Khoảng ngày phải theo thứ tự và không quá 366 ngày")
    return (datetime.combine(start_day, time.min, timezone.utc),
            datetime.combine(end_day + timedelta(days=1), time.min, timezone.utc))


def filters(start: datetime, end: datetime, camera_id: str | None,
            behavior_code: str | None, severity: str | None) -> tuple[str, dict]:
    where = "detected_at >= :start AND detected_at < :end"
    params = {"start": start, "end": end}
    for value, column, key in ((camera_id, "camera_id", "camera_id"),
                               (behavior_code, "behavior_code", "behavior_code"),
                               (severity, "severity", "severity")):
        if value:
            where += f" AND {column} = :{key}"
            params[key] = value
    return where, params


def query_group(db: Connection, where: str, params: dict, columns: str, group: str,
                order: str) -> list[dict]:
    sql = text(f"SELECT {columns}, COUNT(*) AS count FROM alert.alerts WHERE {where} "
               f"GROUP BY {group} ORDER BY {order}")
    return [dict(row) for row in db.execute(sql, params).mappings()]


@router.get("")
def summary(from_date: date | None = Query(None, alias="from"),
            to_date: date | None = Query(None, alias="to"),
            camera_id: str | None = Query(None, alias="cameraId"),
            behavior_code: str | None = Query(None, alias="behaviorCode"),
            severity: str | None = Query(None, pattern="^(INFO|WARNING|CRITICAL)$"),
            _: dict = Depends(require_alert_read), db: Connection = Depends(get_connection)) -> dict:
    start, end = period(from_date, to_date)
    where, params = filters(start, end, camera_id, behavior_code, severity)
    totals = db.execute(text(f"SELECT COUNT(*) AS total, "
                             f"COUNT(*) FILTER (WHERE status = 'FALSE_POSITIVE') AS false_positive, "
                             f"COUNT(*) FILTER (WHERE status IN ('NEW','ASSIGNED','ACKNOWLEDGED')) AS open, "
                             f"COUNT(*) FILTER (WHERE severity = 'CRITICAL') AS critical "
                             f"FROM alert.alerts WHERE {where}"), params).mappings().one()
    daily_rows = query_group(db, where, params, "(detected_at AT TIME ZONE 'UTC')::date AS day",
                             "day", "day")
    daily_counts = {row["day"]: row["count"] for row in daily_rows}
    daily = [{"day": (start.date() + timedelta(days=offset)).isoformat(),
              "count": daily_counts.get(start.date() + timedelta(days=offset), 0)}
             for offset in range((end.date() - start.date()).days)]
    severity_rows = query_group(db, where, params, "severity", "severity", "count DESC, severity")
    status_rows = query_group(db, where, params, "status", "status", "count DESC, status")
    behavior_rows = query_group(db, where, params, "behavior_code AS code, behavior_name AS name",
                                "behavior_code, behavior_name", "count DESC, code")
    camera_rows = query_group(db, where, params, "camera_id AS id, camera_name AS name",
                              "camera_id, camera_name", "count DESC, id")
    return {"from": start.date().isoformat(), "to": (end - timedelta(days=1)).date().isoformat(),
            "timezone": "UTC", "total": totals["total"], "open": totals["open"],
            "critical": totals["critical"], "falsePositive": totals["false_positive"],
            "daily": daily,
            "bySeverity": severity_rows, "byStatus": status_rows,
            "byBehavior": behavior_rows, "byCamera": camera_rows}


@router.get("/export")
def export_excel(from_date: date | None = Query(None, alias="from"),
                 to_date: date | None = Query(None, alias="to"),
                 camera_id: str | None = Query(None, alias="cameraId"),
                 behavior_code: str | None = Query(None, alias="behaviorCode"),
                 severity: str | None = Query(None, pattern="^(INFO|WARNING|CRITICAL)$"),
                 _: dict = Depends(require_alert_read), db: Connection = Depends(get_connection)) -> StreamingResponse:
    start, end = period(from_date, to_date)
    where, params = filters(start, end, camera_id, behavior_code, severity)
    count = db.execute(text(f"SELECT COUNT(*) FROM alert.alerts WHERE {where}"), params).scalar_one()
    if count > 10000:
        raise HTTPException(422, "Có hơn 10.000 cảnh báo; hãy thu hẹp bộ lọc trước khi xuất Excel")
    rows = db.execute(text(f"SELECT id, detected_at, camera_name, place_name, behavior_code, "
                           f"behavior_name, severity, status, zone_id, confidence "
                           f"FROM alert.alerts WHERE {where} ORDER BY detected_at DESC, id DESC"), params)
    book = Workbook(write_only=True)
    sheet = book.create_sheet("Cảnh báo")
    sheet.append(["Mã", "Thời gian UTC", "Camera", "Địa điểm", "Mã hành vi", "Hành vi",
                  "Mức độ", "Trạng thái", "Vùng", "Độ tin cậy"])
    for row in rows:
        sheet.append([excel_text(str(row[0])), row[1].astimezone(timezone.utc).strftime("%Y-%m-%d %H:%M:%S"),
                      excel_text(row[2]), excel_text(row[3]), excel_text(row[4]), excel_text(row[5]),
                      excel_text(row[6]), excel_text(row[7]), excel_text(row[8]), row[9]])
    output = BytesIO()
    book.save(output)
    output.seek(0)
    filename = f"lv-vision-alerts-{start.date()}-{(end - timedelta(days=1)).date()}.xlsx"
    return StreamingResponse(output, media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                             headers={"Content-Disposition": f'attachment; filename="{filename}"'})
