# IdentityService (Full Source) — Username/Email Login + Policy-based RBAC

- ASP.NET Core 8 Web API
- MongoDB cho User/Role/Permission/RefreshToken
- JWT Access Token + Refresh Token (login, refresh, logout/revoke)
- Roles + Permissions
- CRUD Users / Roles / Permissions
- Đăng nhập dùng **UserNameOrEmail** (có thể là username hoặc email)
- RBAC theo **permission** qua `[Authorize(Policy = "...")]`
- UnitTest cho AuthService + Authorization policy

## Chạy Development

1. Chạy Postgress:

```bash
docker  
```

2. Mở `IdentityService.sln` bằng Visual Studio 2022  
3. Set `IdentityService.Api` làm Startup Project  
4. Run (F5), Swagger:

- https://localhost:7002/swagger
- hoặc http://localhost:7001/swagger
- ✔ Tạo migration
dotnet ef migrations add Initial_SqlServer --project IdentityService.Infrastructure --startup-project IdentityService.Api --context IdentityDbContext

- ✔ Apply migration vào DB
dotnet ef database update --project IdentityService.Infrastructure --startup-project IdentityService.Api --context IdentityDbContext    


Lần đầu chạy, seeder sẽ tạo:

- User: `admin`
- Password: `Admin@123`
- Role: `Admin`
- Các permission: `Users.Read`, `Users.Create`, `Users.Update`, `Users.Delete`, ...

## Đăng nhập bằng username hoặc email

```json
POST /api/auth/login
{
  "userNameOrEmail": "admin",
  "password": "Admin@123"
}
```

hoặc

```json
POST /api/auth/login
{
  "userNameOrEmail": "admin@example.com",
  "password": "Admin@123"
}
```

## Policy-based RBAC theo Permission

Trong `Program.cs`:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Users.Read", p => p.RequireClaim("permission", "Users.Read"));
    options.AddPolicy("Users.Create", p => p.RequireClaim("permission", "Users.Create"));
    options.AddPolicy("Users.Update", p => p.RequireClaim("permission", "Users.Update"));
    options.AddPolicy("Users.Delete", p => p.RequireClaim("permission", "Users.Delete"));
});
```

Controller:

```csharp
[Authorize(Policy = "Users.Read")]
[HttpGet]
public async Task<IActionResult> GetPaged(...) { ... }
```

Khi admin login:

- JWT sẽ chứa claim:
  - `role: "Admin"`
  - `permission: "Users.Read"`, `"Users.Create"`, `"Users.Update"`, `"Users.Delete"`, ...

Do đó admin sẽ truy cập được:

- `GET /api/users` (Users.Read)
- `POST /api/users` (Users.Create)
- `PUT /api/users/{id}` (Users.Update)
- `DELETE /api/users/{id}` (Users.Delete)

## UnitTest RBAC

- `AuthServiceTests`: kiểm tra login thành công sẽ gọi `GenerateAccessToken` với danh sách permission lấy từ role.
- `AuthorizationPolicyTests`:
  - User có claim `permission=Users.Read` → policy `"Users.Read"` cho phép.
  - User không có claim → bị từ chối.

