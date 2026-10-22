# DetectionSittingService

FastAPI đọc luồng camera, chạy detector tư thế ngồi trong vùng, lưu minh chứng và phát sự kiện cho AlertService.

- Compose/DNS: `detection-sitting:8001`; endpoint chính `/detect`, `/streams`, `/evidence`.
- Mã chính: `app/main.py`, `app/api/`, `app/domain/`, `app/inference/`, `app/application/`, `app/streaming/`, `app/infrastructure/`, `app/workers/`; các file phẳng cũ chỉ là cầu tương thích. Kiểm thử ở `tests/`.
- Dữ liệu sở hữu: snapshot/clip trong Docker volume `lv_evidence_dev`; sự kiện gửi qua Redis `analytics:events`. Không sở hữu bản ghi camera.
- Cấu hình: `MODEL_PATH`, `MODEL_DEVICE`, `MODEL_IMGSZ`, `CAMERA_SERVICE_URL`, `REDIS_URL`, `EVIDENCE_DIR`, JWT; xem `../docker-compose.dev.yml`.

Từ gốc repo, chạy `python -m unittest discover -s DetectionSittingService/tests -p test_geometry.py -v` và `python DetectionSittingService/tests/verify_phase2b.py` khi stack healthy. Kiểm thử RTSP cần publisher mẫu như [bài học Phase 2B](../docs/learn/phase-2b-detection-sitting.md). Model và giấy phép sử dụng phải được đánh giá riêng trước môi trường thực tế.
