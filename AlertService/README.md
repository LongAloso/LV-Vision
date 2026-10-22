# AlertService

FastAPI nhận sự kiện detector, quản lý cảnh báo, danh mục hành vi và hành động khắc phục.

- Compose/DNS: `alert-service:5102`; API `/api/alerts`, `/api/behaviors`, `/api/remediations`, WebSocket `/ws/alerts`.
- Mã chính: `app/main.py`, `app/routers/`, `app/workers/consumer.py`, `app/store.py`.
- Dữ liệu sở hữu: bảng cảnh báo, danh mục hành vi và khắc phục trong Postgres; đọc stream Redis `analytics:events`.
- Cấu hình: `DATABASE_URL`, `REDIS_URL`, `CAMERA_SERVICE_URL`, `IDENTITY_URL`, JWT; xem `../docker-compose.dev.yml`.

Từ gốc repo, chạy `python AlertService/tests/verify_phase2c.py` khi stack healthy. Xem [bài học Phase 2C](../docs/learn/phase-2c-alert-service.md).
