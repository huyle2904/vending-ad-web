# Development Reference

> Tài liệu nội bộ cho developer. Gộp từ PROJECT_CONTEXT.md, MILESTONES.md, REMAINING_REVIEW_ISSUES.md.
> Cập nhật lần cuối: 2026-05-21

---

## Project Overview

- Repository: `huyle2904/vending-ad-web`
- Solution: `VendingAdSolution/VendingAdSolution.sln`
- Main app: `VendingAdSolution/VendingAdSystem/`
- Worker app: `VendingAdSolution/VendingAdWorker/`
- Stack: ASP.NET Core MVC/Web API trên .NET 8
- Mục đích: CMS quản lý lịch phát video trên màn hình máy bán hàng / TV box

## Domain Model

| Entity | Ý nghĩa |
|---|---|
| `Media` | Thư viện video cá nhân của user |
| `Playlist` | Template nội dung tái sử dụng |
| `PlaybackSchedule` | Kế hoạch phát thực tế áp dụng cho thiết bị |
| `PlaybackScheduleDevice` | Liên kết schedule ↔ device |
| `PlaybackScheduleItem` | Snapshot media có thứ tự trong schedule |

## Business Rules

- User upload video vào thư viện cá nhân
- User tạo playlist template từ video đã upload
- User tạo schedule từ video hoặc một playlist
- Một schedule có thể áp dụng cho nhiều device
- Cùng time window áp dụng cho tất cả device trong schedule
- Khác time window → tạo schedule riêng
- Không phát qua nửa đêm

## Time Rules

- User input/output: giờ Việt Nam
- Lưu DB: UTC
- `StartTime`/`EndTime`: `TimeSpan` theo giờ địa phương Việt Nam
- Dùng `ITimeService.UtcNow` thay vì `DateTime.Now`

## Key File Locations

| Loại | Đường dẫn |
|---|---|
| Controllers | `VendingAdSystem/Controllers/` |
| Services | `VendingAd.Application/Application/Services/` |
| DTOs | `VendingAd.Application/Application/DTOs/` |
| Entities | `VendingAd.Domain/Domain/Entities/` |
| EF Context | `VendingAd.Infrastructure/Infrastructure/Persistence/AppDbContext.cs` |
| DI | `VendingAd.Infrastructure/Infrastructure/DependencyInjection.cs` |
| Migrations | `VendingAd.Infrastructure/Migrations/` |

## Test Accounts

| Role | Username | Password |
|---|---|---|
| Admin | `admin@admin` | `admin@admin` |
| User | `test@test` | `test@test` |
| User mới tạo | — | `TD@12345` (⚠️ cần đổi) |

## Database

- **Local / Codespaces:** SQL Server (Docker container)
- **Production target:** SQL Server
- Connection string: `Server=localhost,1433;Database=VendingAdDb;User Id=sa;Password=VendingAd@12345;TrustServerCertificate=true`
- Startup config keys:
  - `Database:ApplyMigrationsOnStartup`
  - `Database:EnsureCreatedOnStartup`
  - `Database:ResetOnStartup`
  - `Database:ResetSchemaOnStartup`
  - `Seed:EnableDemoData`
  - `Seed:AllowDemoDataOutsideDevelopment`

## Local Infrastructure (Docker Compose)

```bash
# Start tất cả services
docker compose -f docker-compose.infra.yml up -d

# Chỉ start SQL Server
docker compose -f docker-compose.infra.yml up -d sqlserver
```

| Service | URL / Port | Credentials |
|---|---|---|
| SQL Server | `localhost,1433` | `sa` / `VendingAd@12345` |
| Redis | `localhost:6379` | — |
| RabbitMQ | `localhost:5672` | `vendingad` / `vendingad@123` |
| RabbitMQ UI | `http://localhost:15672` | `vendingad` / `vendingad@123` |
| Seq | `http://localhost:5341` | — |
| Prometheus | `http://localhost:9090` | — |
| Grafana | `http://localhost:3000` | `admin` / `vendingad@123` |

## Useful Commands

```bash
# Build
dotnet build "VendingAdSolution/VendingAdSolution.sln"

# Test
dotnet test "VendingAdSolution/VendingAdSolution.sln"

# Run app (default)
dotnet run --no-launch-profile --project "VendingAdSolution/VendingAdSystem"

# Run app với SQL Server
ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=VendingAdDb;User Id=sa;Password=VendingAd@12345;TrustServerCertificate=true" \
dotnet run --no-launch-profile --project "VendingAdSolution/VendingAdSystem"

# Run app với Redis + RabbitMQ
Redis__Enabled=true \
RabbitMQ__Enabled=true \
dotnet run --no-launch-profile --project "VendingAdSolution/VendingAdSystem"

# Run worker
dotnet run --project "VendingAdSolution/VendingAdWorker"

# Apply migrations thủ công
export PATH="$PATH:$HOME/.dotnet/tools"
dotnet ef database update \
  --project VendingAdSolution/VendingAd.Infrastructure \
  --startup-project VendingAdSolution/VendingAdSystem
```

## Load Testing (k6)

```bash
# Cài k6
brew install k6  # macOS
# hoặc: https://k6.io/docs/get-started/installation/

# Smoke test (1 VU, 1 phút — sanity check)
k6 run k6/smoke.js

# Load test (ramp lên 50 VUs, ~10 phút)
k6 run k6/load.js

# Stress test (ramp lên 200 VUs — tìm breaking point)
k6 run k6/stress.js

# Chạy với app trên server khác
BASE_URL=https://your-app.onrender.com DEVICE_CODE=ABC123 DEVICE_SECRET=secret k6 run k6/load.js
```

| Script | VUs | Duration | Mục đích |
|---|---|---|---|
| `smoke.js` | 1 | 1m | Sanity check sau deploy |
| `load.js` | 50 | ~10m | Kiểm tra normal load |
| `stress.js` | 200 | ~17m | Tìm breaking point |

Thresholds mặc định: p95 < 2s, error rate < 5%.

## Health Checks

```bash
curl http://localhost:8080/health/live
curl http://localhost:8080/health/ready
```

- `/health/live`: process đang chạy
- `/health/ready`: DB + Redis (nếu enabled) + RabbitMQ (nếu enabled)

## Redis Cache Keys

| Key | Mục đích |
|---|---|
| `mobile:playback-state:{deviceCode}` | Response cache per device |
| `mobile:device-active-schedule:{deviceCode}` | Active schedule mapping |
| `mobile:schedule-content:{scheduleId}:{version}` | Shared schedule payload |
| `lock:mobile:schedule-content:{scheduleId}:{version}` | Distributed lock chống stampede |
| `device:online:{deviceCode}` | Device presence TTL |

## Mobile API

| Endpoint | Mô tả |
|---|---|
| `GET /api/mobile/devices/{deviceCode}` | Device info |
| `POST /api/mobile/heartbeat` | Heartbeat |
| `GET /api/mobile/playback-state/{deviceCode}` | Playback state |

- Yêu cầu `X-Device-Secret` hoặc `Authorization: Bearer <secret>`
- Rate limit theo `deviceCode`

---

## Milestones

### ✅ Hoàn thành

| Milestone | Mô tả |
|---|---|
| 0 | Mobile/TV Box API Foundation |
| 1 | Database Indexing |
| 2A | Mobile Read Query Optimization (AsNoTracking) |
| 2B | Portal/Admin Read Query Optimization |
| 3 | Redis Playback-State Cache |
| 3.5 | Shared Schedule Playback Cache |
| 4 | Redis Device Presence / Heartbeat |
| 5 | Mobile API Rate Limiting |
| 6 | Local Infrastructure Expansion (Docker Compose) |
| 6.5 | SQL Server Migration Readiness |
| 7 | RabbitMQ Infrastructure |
| 8 | Worker Service |
| 9 | Event-Driven Schedule Cache Invalidation |
| 9.5 | E2E Stabilization và Health Checks |
| 9.6 | Local SQL Server Readiness |
| 9.7 | Security Hardening Baseline |
| — | UI/UX CMS Improvements |

### 🔄 Phase 1 (Production Readiness) — đang làm

Xem chi tiết trong `production-readiness-analysis.html`.

| Task | Mô tả | Status |
|---|---|---|
| 1.1 | Serilog Structured Logging | ✅ Done |
| 1.2 | Correlation ID Middleware | ✅ Done |
| 1.3 | Fix Default Password | Pending |
| 1.4 | Audit Logging | Pending |
| 1.5 | Global Exception Handling | Pending |
| 1.6 | File Storage Abstraction | Pending |
| 1.7 | Prometheus Metrics | Pending |
| 1.8 | Grafana Dashboards | Pending |
| 1.9 | Load Testing (k6) | Pending |
| 1.10 | SQL Server Migration Testing | Pending |
| 1.11 | Production Config Hardening | Pending |
| 1.12 | Security Headers | Pending |
| 1.13 | Database Backup Strategy | Pending |
| 1.14 | Deployment Documentation | Pending |
| 1.15 | Production Smoke Tests | Pending |

### 📋 Planned

| Milestone | Mô tả |
|---|---|
| 10 | Video Metadata / Thumbnail Pipeline |
| 11 | Object Storage + CDN |
| 12 | Observability (Serilog/Seq/OpenTelemetry) |
| 13 | Load Testing (k6/NBomber) |

---

## Known Issues & Technical Debt

### 🔴 Cần làm trước production

**1. Mật khẩu mặc định cố định (`TD@12345`)**
- File: `AdminController.cs`
- Vấn đề: Tạo user và reset password dùng chung password cố định
- Cần làm: Generate password ngẫu nhiên, hiển thị một lần, thêm flag `MustChangePassword`

**2. Audit log cho thao tác nhạy cảm**
- Chưa có audit trail cho: login/logout, tạo/reset user, rotate/revoke device secret, upload/delete video, thay đổi schedule
- Cần làm: Tạo bảng `AuditLogs`, ghi actor/action/target/timestamp/IP

**3. Device secret lifecycle thiếu audit**
- File: `DeviceCredentialService.cs`
- Rotate/revoke đã có nhưng chưa ghi audit trail

### 🟡 Nên làm sớm

**4. Session/manual auth check lẫn với Cookie Auth**
- Files: `AdminController.cs`, `PortalController.cs`, `CurrentSession.cs`
- Nhiều action vẫn tự check `_currentSession` thay vì dùng `[Authorize]`
- Cần làm: Chuyển sang policy/role-based authorization

**5. Upload video cần validation sâu hơn**
- File: `MediaUploadService.cs`
- Chưa có: malware scan, cleanup job cho orphan files
- ffprobe đã đủ cho giai đoạn hiện tại

**6. Thiếu integration tests**
- Cần thêm: test `/dashboard` redirect theo role, anonymous/user/admin trên portal/admin routes, POST thiếu anti-forgery token

### 🟢 Production secret management

- Dùng environment variables hoặc secret manager
- Không commit `appsettings.Development.json` chứa secret thật
- CI/CD set `ConnectionStrings`, RabbitMQ, Redis qua repository secrets

---

## Architecture Notes

### Event-Driven Flow

```
Web (schedule change)
  → DB save
  → Publish ScheduleChangedEvent (RabbitMQ)
  → Worker consumes
  → Invalidate per-device cache keys
  → Warm schedule-content cache cho active schedules
```

### Cache Strategy

- Per-device response cache giảm polling load
- Shared schedule cache cho nhiều device cùng schedule
- Redis distributed lock chống cache stampede
- Device presence TTL giảm DB writes từ heartbeat

### Communication

- Nói chuyện với user bằng tiếng Việt
- Code identifiers bằng tiếng Việt
