# Project Milestones

This file tracks the main technical milestones implemented in the VendingAd project and the next production-readiness steps.

## Done

### Milestone 0: Mobile/TV Box API Foundation

Status: Done

Goal: Add backend APIs for future mobile/TV box devices.

Implemented:

- `GET /api/mobile/devices/{deviceCode}`
- `POST /api/mobile/heartbeat`
- `GET /api/mobile/playback-state/{deviceCode}`
- Device claim status handling
- Active schedule lookup
- Schedule version and ordered playback items

Key files:

- `Application/DTOs/MobilePlaybackDtos.cs`
- `Application/Services/MobilePlaybackService.cs`
- `Controllers/MobileApiController.cs`

---

### Milestone 1: Database Indexing

Status: Done

Goal: Improve lookup performance for device, schedule, media, and playlist-related queries.

Implemented:

- Unique index for `Devices.DeviceCode`
- Indexes for device user/status/claim lookup
- Indexes for schedule active/date/user queries
- Indexes for schedule-device and schedule-item lookups
- Indexes for media user/upload date queries

Key file:

- `Infrastructure/Persistence/AppDbContext.cs`

Notes:

- These EF Core indexes are provider-friendly for SQLite/PostgreSQL/SQL Server.
- Existing production databases may still need migrations or provider-safe index scripts.

---

### Milestone 2A: Mobile Read Query Optimization

Status: Done

Goal: Reduce EF Core tracking overhead for high-frequency mobile read APIs.

Implemented:

- Added `AsNoTracking()` to mobile read-only queries.
- Kept tracking for heartbeat because it updates `Device.LastSeen`.

Key file:

- `Application/Services/MobilePlaybackService.cs`

---

### Milestone 2B: Portal/Admin Read Query Optimization

Status: Done

Goal: Apply safe `AsNoTracking()` usage to CMS/admin read-only pages.

Implemented:

- Optimized portal dashboard/video/device/playlist/schedule list queries.
- Optimized admin dashboard/devices/videos/playlists/schedules/users list queries.
- Kept tracking for update/delete/toggle/assign/reorder flows.

Key files:

- `Controllers/PortalController.cs`
- `Controllers/AdminController.cs`
- `Controllers/PortalApiController.cs`
- `Application/Services/PlaybackScheduleService.cs`
- `Application/Services/PlaylistManagementService.cs`

---

### Milestone 3: Redis Playback-State Cache

Status: Done

Goal: Cache high-frequency mobile playback-state responses.

Implemented:

- Cache abstraction:
  - `ICacheService`
  - `MemoryCacheService`
  - `RedisCacheService`
- Redis config via `Redis:Enabled` and `Redis:ConnectionString`
- Device playback-state cache:
  - `mobile:playback-state:{deviceCode}`
- Local Redis infrastructure via Docker Compose.

Key files:

- `Application/Services/CacheService.cs`
- `Application/Services/MobilePlaybackService.cs`
- `Infrastructure/DependencyInjection.cs`
- `docker-compose.infra.yml`

Notes:

- Redis is disabled by default in local config, and can be enabled with `Redis__Enabled=true`.
- This milestone reduces repeated polling load per device.

---

### Milestone 3.5: Shared Schedule Playback Cache

Status: Done

Goal: Support many devices running the same schedule without repeated DB-heavy schedule/item queries.

Problem solved:

- If 200 devices run the same schedule, they should share one cached schedule content payload instead of each request loading schedule items/media from DB.

Implemented Redis keys:

- `mobile:playback-state:{deviceCode}`
- `mobile:device-active-schedule:{deviceCode}`
- `mobile:schedule-content:{scheduleId}:{version}`
- `lock:mobile:schedule-content:{scheduleId}:{version}`

Implemented:

- Shared schedule content cache by `scheduleId + version`
- Device active schedule mapping cache
- Redis distributed lock to reduce cache stampede risk
- Cache warm/invalidate when schedules are created, updated, toggled, deleted, or reordered
- Inline comments in Redis-related files to explain the flow

Key files:

- `Application/DTOs/MobilePlaybackDtos.cs`
- `Application/Services/CacheService.cs`
- `Application/Services/MobilePlaybackCacheService.cs`
- `Application/Services/MobilePlaybackService.cs`
- `Application/Services/PlaybackScheduleService.cs`
- `Infrastructure/DependencyInjection.cs`

Validation:

- Tested two devices attached to the same schedule.
- Confirmed both devices returned the same schedule version.
- Confirmed Redis created `mobile:schedule-content:*` shared cache key.

---

### UI/UX CMS Improvements

Status: In Progress

Goal: Improve CMS usability while keeping the existing light, blue, simple visual language.

Implemented:

- Better date/time display with highlighted chips
- Profile/settings placeholder pages
- Thumbnail fallback for video display
- Phase 1 polish for Video and Playlist pages

Key files:

- `Views/Portal/Videos.cshtml`
- `Views/Portal/Playlist.cshtml`
- `Views/Profile/Index.cshtml`
- `Views/Settings/Index.cshtml`
- `wwwroot/css/site.css`
- `wwwroot/images/video-placeholder.svg`

Notes:

- Video thumbnail fallback is UI-only for now.
- Real thumbnail generation is planned for the worker/media pipeline milestone.

### Milestone 4: Redis Device Presence / Heartbeat

Status: Done

Goal: Reduce DB writes from heartbeat and track online/offline devices with Redis TTL.

Implemented:

- Set `device:online:{deviceCode}` with TTL on heartbeat.
- Update DB `LastSeen` only when enough time has passed.
- Use presence service for dashboard/admin online/offline state, with DB `LastSeen` fallback.
- Configurable presence TTL and DB write interval via `DevicePresence` settings.

Key files:

- `Application/Services/DevicePresenceService.cs`
- `Application/Services/CacheService.cs`
- `Application/Services/MobilePlaybackService.cs`
- `Controllers/PortalController.cs`
- `Controllers/AdminController.cs`
- `Controllers/PortalApiController.cs`

Notes:

- Default online TTL is 90 seconds.
- Default DB write interval is 60 seconds.
- This keeps dashboards responsive while avoiding one DB write per heartbeat.

---

## Next Milestones

---

### Milestone 5: Mobile API Rate Limiting

Status: Done

Goal: Protect high-frequency mobile APIs from retry storms or buggy clients.

Implemented:

- Rate limit `/api/mobile/heartbeat`.
- Rate limit `/api/mobile/playback-state/{deviceCode}`.
- Device-code based partitioning instead of IP-only limiting.
- Configurable fixed-window limits via `MobileRateLimiting` settings.
- HTTP `429 Too Many Requests` response with `Retry-After` when a device exceeds its limit.

Key files:

- `Application/Services/MobileRateLimitService.cs`
- `Filters/MobileRateLimitAttribute.cs`
- `Controllers/MobileApiController.cs`
- `Infrastructure/DependencyInjection.cs`
- `appsettings.json`

Notes:

- Default heartbeat limit is 10 requests per 60 seconds per device.
- Default playback-state limit is 30 requests per 60 seconds per device.
- Current limiter is in-process, matching the current single web app deployment model.
- If production runs multiple backend instances later, move counters to Redis for distributed enforcement.

---

### Milestone 6: Local Infrastructure Expansion

Status: Done

Goal: Expand Codespaces/local Docker Compose infrastructure for learning and production-like testing.

Implemented services:

- Redis
- SQL Server 2022 Developer
- RabbitMQ
- Seq logging UI

Key file:

- `docker-compose.infra.yml`

Notes:

- SQLite remains the default app database for quick startup.
- SQL Server is available through Docker for production-like testing and future MSSQL target work.
- RabbitMQ is available for the next event-driven architecture milestone.
- Seq is available for future structured logging/observability work.
- MinIO is intentionally deferred until the object storage milestone.

---

### Milestone 6.5: SQL Server Migration Readiness

Status: Done

Goal: Make SQL Server setup reproducible with EF Core migrations instead of relying only on `EnsureCreated()`.

Implemented:

- Added initial EF Core migration for SQL Server schema.
- Startup now uses `Database.Migrate()` when `DatabaseProvider=SqlServer`.
- SQLite quick-dev mode still uses `EnsureCreated()`.
- Seeder now handles data only; schema creation is handled outside the seeder.
- Verified migration against a clean SQL Server database.
- Verified app startup seeds admin/user/device data after migration.

Key files:

- `Infrastructure/Persistence/Migrations/20260514031300_InitialSqlServerSchema.cs`
- `Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs`
- `Infrastructure/Seed/DatabaseSeeder.cs`
- `Program.cs`

Validation:

- Applied migration to clean `VendingAdDbMigrationTest` database.
- Confirmed tables were created in SQL Server.
- Confirmed app startup returned login redirect and seeded 1 admin, 1 user, and 4 devices.

---

### Milestone 7: RabbitMQ Infrastructure

Status: Done

Goal: Introduce event-driven architecture for async background work.

Implemented:

- `IMessagePublisher`
- RabbitMQ publisher implementation
- Event contracts such as `ScheduleChangedEvent` and `VideoUploadedEvent`
- RabbitMQ config via `RabbitMQ:Enabled`, host, credentials, and exchange name.
- `NullMessagePublisher` fallback when RabbitMQ is disabled.
- Schedule create/update/delete/toggle/item reorder flows publish `ScheduleChangedEvent`.

Key files:

- `Application/Messaging/IntegrationEvents.cs`
- `Application/Messaging/MessagePublisher.cs`
- `Application/Services/PlaybackScheduleService.cs`
- `Infrastructure/DependencyInjection.cs`
- `appsettings.json`

Notes:

- RabbitMQ is disabled by default so local quick-start remains simple.
- Publisher failures are logged as warnings and do not break successful DB changes.
- Worker consumption remains planned for Milestone 8.

---

### Milestone 8: Worker Service

Status: Done

Goal: Move background work out of web requests.

Implemented:

- Add `VendingAdWorker` project
- Consume RabbitMQ events
- Add shared `VendingAd.Contracts` project for integration event contracts.
- Consume `ScheduleChangedEvent` from queue `vendingad.worker.schedule-changed`.
- Bind queue to exchange `vendingad.events` with routing key `schedule.changed`.
- Log consumed schedule events and acknowledge successful messages.

Key files:

- `VendingAd.Contracts/IntegrationEvents.cs`
- `VendingAdWorker/Worker.cs`
- `VendingAdWorker/RabbitMqWorkerOptions.cs`
- `VendingAdWorker/appsettings.json`
- `VendingAdSolution.sln`

Validation:

- Started RabbitMQ container.
- Published a test `ScheduleChangedEvent`.
- Confirmed worker consumed and acknowledged the event.
- Confirmed queue `vendingad.worker.schedule-changed` returned to 0 ready messages.

Deferred:

- Warm/invalidate cache in background moves to Milestone 9.
- Process media metadata later.

---

### Milestone 9: Event-Driven Schedule Cache Invalidation

Status: Done

Goal: Move schedule cache warm/invalidate flow from web request to RabbitMQ + Worker.

Implemented:

- Split shared code into layered class libraries:
  - `VendingAd.Domain`
  - `VendingAd.Application`
  - `VendingAd.Infrastructure`
- Web schedule write flows save DB changes first, then publish `ScheduleChangedEvent`.
- Removed direct web-request calls to schedule cache refresh.
- `ScheduleChangedEvent.AffectedDeviceCodes` now means all devices affected before or after the change.
- Schedule reassignment publishes the union of old and new device codes.
- Worker consumes `ScheduleChangedEvent` and delegates cache handling to `IScheduleCacheEventHandler`.
- Worker invalidates per-device playback cache keys for every affected device.
- Worker warms `mobile:schedule-content:*` only for schedules that still exist and are active.
- Deleted or inactive schedules skip cache warm.
- Handler logs cache failures and does not throw back into RabbitMQ processing; worker acknowledges and relies on TTL fallback.
- Worker infrastructure fails fast if Redis is not enabled.
- Added focused xUnit tests for cache handler behavior, cache key invalidation, and schedule reassignment event semantics.
- Added separate CI/publish flow for web and worker artifacts.
- Added `global.json` pinned to .NET 8 with roll-forward.

Key files:

- `VendingAdSolution/VendingAd.Application/Application/Services/PlaybackScheduleService.cs`
- `VendingAdSolution/VendingAd.Application/Application/Services/ScheduleCacheEventHandler.cs`
- `VendingAdSolution/VendingAd.Application/Application/Services/MobilePlaybackCacheService.cs`
- `VendingAdSolution/VendingAd.Infrastructure/Infrastructure/DependencyInjection.cs`
- `VendingAdSolution/VendingAdWorker/Worker.cs`
- `VendingAdSolution/VendingAd.Tests/`
- `.github/workflows/ci.yml`
- `.github/workflows/publish.yml`

Validation:

- `dotnet restore`
- `dotnet build`
- `dotnet test`
- publish web artifact
- publish worker artifact

---

### Milestone 9.5: E2E Stabilization and Health Checks

Status: Done

Goal: Make the event-driven cache flow testable end-to-end and expose production-style dependency health.

Implemented:

- Added web health endpoints:
  - `GET /health/live`
  - `GET /health/ready`
- Readiness checks cover:
  - Database connectivity
  - Redis connectivity when `Redis:Enabled=true`
  - RabbitMQ connectivity when `RabbitMQ:Enabled=true`
- Local quick-start remains healthy when Redis/RabbitMQ are disabled by configuration.
- Worker now validates startup dependencies before consuming messages:
  - Database reachable
  - Redis reachable
  - RabbitMQ reachable
- Worker validates required RabbitMQ options on startup.
- Added JSON health response output for easier manual and automated checks.
- Documented E2E verification commands for Redis + RabbitMQ + worker cache invalidation.

Key files:

- `VendingAdSolution/VendingAd.Infrastructure/Infrastructure/Health/`
- `VendingAdSolution/VendingAd.Infrastructure/Infrastructure/DependencyInjection.cs`
- `VendingAdSolution/VendingAdSystem/Program.cs`
- `VendingAdSolution/VendingAdWorker/Program.cs`
- `PROJECT_CONTEXT.md`

Validation:

- `dotnet restore`
- `dotnet build --configuration Release`
- `dotnet test --configuration Release --no-build`

---

### Milestone 10: Video Metadata / Thumbnail Pipeline

Status: Planned

Goal: Generate real video thumbnails and metadata asynchronously.

Planned:

- Publish `VideoUploadedEvent`
- Worker uses FFmpeg/ffprobe or equivalent
- Fill `Media.ThumbnailUrl`
- Fill duration/checksum fields later

---

### Milestone 11: Object Storage + CDN

Status: Planned

Goal: Move video files away from the ASP.NET web server for production scale.

Planned:

- Add `IFileStorageService`
- Local storage provider
- S3-compatible provider such as Cloudflare R2 or AWS S3
- CDN-backed video delivery

---

### Milestone 12: Observability

Status: Planned

Goal: Improve production debugging and monitoring.

Planned:

- Structured logging
- Health checks for DB/Redis/RabbitMQ/storage
- Request latency logging
- Optional Sentry/Seq/OpenTelemetry

---

### Milestone 13: Load Testing

Status: Planned

Goal: Prove API behavior under 100-200 simulated devices.

Planned:

- k6 or NBomber tests
- Playback-state polling scenario
- Heartbeat scenario
- Schedule update fan-out scenario
- Measure p95/p99 latency and error rate
