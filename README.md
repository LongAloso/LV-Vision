# LV-Vision

Monorepo giám sát camera và cảnh báo hành vi. [ROADMAP](docs/ROADMAP.md) là nguồn yêu cầu theo giai đoạn; [API contract](docs/API-CONTRACT.md) mô tả giao tiếp giữa các service. Tài liệu hướng dẫn code và kết quả kiểm tra nằm trong [docs/learn](docs/learn/README.md).

## Cấu trúc đang chạy

| Thư mục mã nguồn | Compose service | Vai trò |
|---|---|---|
| [ApiGateway](ApiGateway/) | `apigateway` | Cổng API Ocelot, port 8888 |
| [IdentityService](IdentityService/) | `identityservice` | Đăng nhập, JWT và phân quyền |
| [CameraService](CameraService/) | `camera-service` | Camera, địa điểm, kịch bản và vùng |
| [DetectionSittingService](DetectionSittingService/) | `detection-sitting` | Phát hiện ngồi trong vùng giám sát |
| [AlertService](AlertService/) | `alert-service` | Cảnh báo, danh mục hành vi và khắc phục |
| [NotificationService](NotificationService/) | `notification-service` | Thông báo và WebSocket |
| [SystemService](SystemService/) | `system-service` | Tổng hợp trạng thái hệ thống |
| [web](web/) | `web` | Giao diện Vue |

Tên thư mục dùng PascalCase theo quy ước mã nguồn. Tên Compose, hostname nội bộ và URL API là hợp đồng runtime riêng; đổi thư mục không đổi các tên đó. Các service của giai đoạn sau chỉ được tạo khi triển khai giai đoạn tương ứng.

## Chạy môi trường phát triển

```powershell
docker compose -f docker-compose.dev.yml config --quiet
docker compose -f docker-compose.dev.yml up -d --build
docker compose -f docker-compose.dev.yml ps
```

Các biến môi trường mẫu ở [.env.example](.env.example). Gateway: `http://localhost:8888`; web: `http://localhost:3000`. Compose quản lý Postgres, SQL Server, Redis, RabbitMQ và các service. Không chạy `down -v` nếu muốn giữ dữ liệu dev.

Xem [hướng dẫn tái cấu trúc](docs/learn/repository-microservice-restructure.md) để hiểu đường dẫn build, script kiểm thử và giới hạn của thay đổi này.
