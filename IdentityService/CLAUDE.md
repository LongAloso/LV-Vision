# IdentityService — Backend (scoped)

Auto-load khi chạm service này. Quy ước back chung: [root CLAUDE.md](../CLAUDE.md) §"Bố cục & invariant backend" — file này KHÔNG lặp lại. Review sâu: subagent `dotnet-reviewer`.

**Vai trò**: Auth, user, role (JWT) · port host **5000**.

## Đặc thù — tránh lỗi lặp
- **Config-center**: service này là nguồn config runtime cho toàn hệ — `Services:<ServiceName>:*` trong `appsettings` của nó cấp `ConnectionStrings:SpcDatabase`, `Database:Provider`, JWT, RabbitMQ… cho các service khác. Đổi DB/endpoint sửa **ở đây**, không sửa lẻ từng service. Giá trị hiệu lực đọc từ `appsettings.Development.json` (gitignored) hoặc env override — **đừng chép host/DB/credential vào file commit**; mẫu ở `appsettings.example.json`.
- Tên service trong key ConfigCenter lấy từ **assembly name** qua `NormalizeServiceName()` — sau đợt bỏ tiền tố `Spc.`, key là `Services:ConfigService`, không phải `Services:Spc.ConfigService`.
- **Ngoại lệ EF provider**: đây là service duy nhất còn fallback `"Postgres"` khi thiếu `Database:Provider` (các service khác fallback `"SqlServer"`) → chạy SQL Server thì phải set key rõ ràng, nếu không sẽ `UseNpgsql` và fail lúc mở connection.
- KHÔNG commit `appsettings.*.json` / secrets (dùng `*.example.*`). Test login thật: chỉ dùng credential do user cung cấp trong phiên; access token FE lưu ở `sessionStorage['auth.accessToken']`.

## Build / test
```powershell
dotnet build IdentityService/IdentityService.sln
dotnet test  IdentityService/IdentityService.Tests/IdentityService.Tests.csproj
```
