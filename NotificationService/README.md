# NotificationService

FastAPI tạo thông báo từ cảnh báo và đẩy cập nhật trực tiếp tới giao diện.

- Compose/DNS: `notification-service:5103`; API `/api/notifications`, WebSocket `/ws/notifications`.
- Mã chính: `app/main.py`, `app/store.py`, `app/live.py`.
- Dữ liệu sở hữu: bảng thông báo trong Postgres; đọc luồng sự kiện Redis theo cấu hình Compose.
- Cấu hình: `DATABASE_URL`, `REDIS_URL`, `IDENTITY_URL`, JWT; xem `../docker-compose.dev.yml`.

Từ gốc repo, chạy `python NotificationService/tests/verify_phase2d.py` khi stack healthy và có RTSP publisher mẫu. Xem [bài học Phase 2D](../docs/learn/phase-2d-web-integration.md).
