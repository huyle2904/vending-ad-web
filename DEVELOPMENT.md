# Development Reference

> Tài liệu nội bộ cho developer. 
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

- Demo accounts are allowed only in disposable Development environments.
- Admin-created users receive a random temporary password that is displayed once.
- Never reuse demo credentials in a shared or production environment.

## Database

- **Local / Codespaces:** PostgreSQL or SQL Server
- **Production target:** configurable per environment
- PostgreSQL connection string: `Host=localhost;Port=5432;Database=vendingad;Username=vendingad;Password=<your-password>`
- SQL Server connection string: `Server=localhost,1433;Database=VendingAdDb;User Id=sa;Password=<your-password>;TrustServerCertificate=true`
- Startup config keys:
  - `DatabaseProvider`
  - `Database:ApplyMigrationsOnStartup`

## Local Infrastructure (Docker Compose)

```bash
# Start tất cả services
docker compose -f docker-compose.infra.yml up -d

# Chỉ start PostgreSQL
docker compose -f docker-compose.infra.yml up -d postgres

# Chỉ start SQL Server
docker compose -f docker-compose.infra.yml up -d sqlserver
```

| Service | URL / Port | Credentials |
|---|---|---|
| SQL Server | `localhost,1433` | Configure in `.env` |
| Redis | `localhost:6379` | None by default |
| RabbitMQ | `localhost:5672` | Configure in `.env` |
| RabbitMQ UI | `http://localhost:15672` | Configure in `.env` |
| Seq | `http://localhost:5341` | None by default |
| Prometheus | `http://localhost:9090` | None by default |
| Grafana | `http://localhost:3000` | Configure in `.env` |

## Useful Commands

```bash
# Build
dotnet build "VendingAdSolution/VendingAdSolution.sln"

# Test
dotnet test "VendingAdSolution/VendingAdSolution.sln"

# Run app (default)
dotnet run --no-launch-profile --project "VendingAdSolution/VendingAdSystem"

# Run app với SQL Server
ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=VendingAdDb;User Id=sa;Password=<local-sqlserver-password>;TrustServerCertificate=true" \
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

### Production Readiness

The current handover and release requirements are maintained in `HANDOVER.md`, `DEPLOYMENT.md`, and `RUNBOOK.md`.
Structured logging, correlation IDs, audit logging, exception handling, storage abstraction, metrics, security headers, temporary password generation, CI, load-test scripts, and smoke-test scripts are implemented.
Production ownership transfer, credential rotation, backup restore verification, and Android release-key transfer remain operational checklist items.

### 📋 Planned

| Milestone | Mô tả |
|---|---|
| 10 | Video Metadata / Thumbnail Pipeline |
| 11 | Object Storage + CDN |
| 12 | Observability (Serilog/Seq/OpenTelemetry) |
| 13 | Load Testing (k6/NBomber) |

---

## Known Technical Debt

- Some controllers still retain session compatibility checks in addition to role authorization.
- Uploaded media has format and ffprobe validation but no malware scanning service.
- Object storage and CDN support are not implemented.
- Multi-instance deployments need shared Data Protection keys and distributed infrastructure enabled.
- Production credentials, ownership, backup restore, and incident contacts must be completed in `HANDOVER.md`.

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
