# SystemService

FastAPI tổng hợp tình trạng các service, Redis và kho minh chứng cho màn hình hệ thống.

- Compose/DNS: `system-service:5108`; API `/api/system/health`, healthcheck `/health`.
- Mã chính: `app/main.py`; không có database riêng.
- Đọc trạng thái qua HTTP nội bộ, Redis và Docker volume minh chứng; không sửa dữ liệu của service khác.
- Cấu hình: `REDIS_URL`, `EVIDENCE_DIR`, `INTERNAL_API_KEY`, `IDENTITY_URL`, JWT; xem `../docker-compose.dev.yml`.

Từ gốc repo, chạy `docker compose -f docker-compose.dev.yml ps` và gọi `/systemservice/system/health` qua Gateway với JWT hợp lệ. Xem [kiến trúc](../docs/ARCHITECTURE.md).
