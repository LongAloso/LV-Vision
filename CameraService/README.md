# CameraService

FastAPI quản lý camera, địa điểm, kịch bản, vùng giám sát và trạng thái camera. Một camera có thể được gán nhiều kịch bản theo lựa chọn; tạo camera không tự gán kịch bản ngồi lối đi.

- Compose/DNS: `camera-service:5101`; API qua Gateway: `/cameraservice/...` (xem [API contract](../docs/API-CONTRACT.md)).
- Mã chính: `app/main.py`, `app/api/`, `app/services/`, `app/infrastructure/db/`, `app/realtime/`; các file phẳng cũ chỉ là cầu tương thích. Kiểm thử: `tests/verify_phase2a.py`.
- Dữ liệu sở hữu: bảng camera, địa điểm, kịch bản và liên kết camera–kịch bản trong Postgres; khóa RTSP bằng `CAMERA_RTSP_KEY`.
- Cấu hình và dependency: `../docker-compose.dev.yml`, `requirements.txt`, `Dockerfile`. JWT và `IDENTITY_URL` phục vụ xác thực; `DATABASE_URL` trỏ đến Postgres.

Từ gốc repo, chạy `python CameraService/tests/verify_phase2a.py` khi stack Compose đã healthy. Xem [bài học Phase 2A](../docs/learn/phase-2a-camera-service.md).
