# Project Context

## Overview

- Repository: `/workspaces/VendingCMS`
- Solution: `VendingAdSolution/VendingAdSolution.sln`
- Main app: `VendingAdSolution/VendingAdSystem/VendingAdSystem.csproj`
- Worker app: `VendingAdSolution/VendingAdWorker/VendingAdWorker.csproj`
- Shared contracts: `VendingAdSolution/VendingAd.Contracts/VendingAd.Contracts.csproj`
- Domain library: `VendingAdSolution/VendingAd.Domain/VendingAd.Domain.csproj`
- Application library: `VendingAdSolution/VendingAd.Application/VendingAd.Application.csproj`
- Infrastructure library: `VendingAdSolution/VendingAd.Infrastructure/VendingAd.Infrastructure.csproj`
- Stack: ASP.NET Core MVC/Web API on .NET 8
- Active branch: `dev`
- Primary product: CMS for managing video playback schedules on vending machine displays / TV box devices

## Communication

- Talk to the user in Vietnamese.
- Keep internal code identifiers in English.
- The user is learning production backend architecture and benefits from clear, beginner-friendly explanations.

## Domain Meaning

- `Media` = uploaded personal video library
- `Playlist` = reusable content template only
- `PlaybackSchedule` = actual playback plan applied to devices
- `PlaybackScheduleDevice` = link between schedule and device
- `PlaybackScheduleItem` = ordered snapshot of media in a schedule

## Business Rules

- User uploads videos into personal library.
- User creates playlist templates from uploaded videos.
- User creates schedules from selected videos or one playlist.
- A schedule can apply to multiple devices.
- Same schedule time window applies to all selected devices.
- Different time windows require separate schedules.
- No cross-midnight playback.

## Time Rules

- User input/output is Vietnam time.
- Persist `DateTime` in UTC.
- `StartTime` / `EndTime` are stored as `TimeSpan` for Vietnam local day time.
- Use `ITimeService.UtcNow` instead of `DateTime.Now` for business logic.

## Key Files

- Controllers: `VendingAdSolution/VendingAdSystem/Controllers/`
- Services: `VendingAdSolution/VendingAd.Application/Application/Services/`
- DTOs: `VendingAdSolution/VendingAd.Application/Application/DTOs/`
- Entities: `VendingAdSolution/VendingAd.Domain/Domain/Entities/`
- EF context: `VendingAdSolution/VendingAd.Infrastructure/Infrastructure/Persistence/AppDbContext.cs`
- DI: `VendingAdSolution/VendingAd.Infrastructure/Infrastructure/DependencyInjection.cs`
- Main CSS: `VendingAdSolution/VendingAdSystem/wwwroot/css/site.css`
- Milestone tracker: `MILESTONES.md`

## Accounts

- Admin: `admin@admin` / `admin@admin`
- Demo user: `test@test` / `test@test`
- Admin-created user default password: `TD@12345`

## Database / Deploy State

- Local default DB: SQLite
- Render temporary production DB: PostgreSQL
- Future target DB: SQL Server
- Config key: `DatabaseProvider` supports `Sqlite`, `Postgres`, `SqlServer`
- Codespaces/local should keep SQLite as default for quick startup, and use SQL Server from Docker Compose when production-like testing is needed.
- SQL Server now has EF Core migrations under `Infrastructure/Persistence/Migrations`.
- Startup behavior is controlled by config:
  - `Database:ApplyMigrationsOnStartup`
  - `Database:EnsureCreatedOnStartup`
  - `Seed:EnableDemoData`
- SQL Server local/dev should use migrations.
- SQLite quick-dev mode can use `EnsureCreated()`.

## Mobile API State

Main endpoints:

- `GET /api/mobile/devices/{deviceCode}`
- `POST /api/mobile/heartbeat`
- `GET /api/mobile/playback-state/{deviceCode}`

Main files:

- `Application/DTOs/MobilePlaybackDtos.cs`
- `Application/Services/MobilePlaybackService.cs`
- `Application/Services/MobilePlaybackCacheService.cs`
- `Controllers/MobileApiController.cs`

Playback-state currently returns:

- `success`
- `deviceCode`
- `serverTimeUtc`
- `hasActiveSchedule`
- `claimRequired`
- `claimCode`
- `schedule`
- `items`

## Redis / Shared Schedule Cache

Redis is used to reduce repeated playback-state work.

Config in `appsettings.json`:

```json
"Redis": {
  "Enabled": false,
  "ConnectionString": "localhost:6379"
}
```

Local infrastructure helper:

- Compose file: `docker-compose.infra.yml`
- Start: `docker compose -f docker-compose.infra.yml up -d`
- Check containers: `docker compose -f docker-compose.infra.yml ps`
- Check: `docker exec vendingad-redis redis-cli ping`

Local services in Compose:

- Redis: `localhost:6379`
- SQL Server: `localhost,1433` / user `sa` / password `VendingAd@12345`
- RabbitMQ: `localhost:5672`, management UI `http://localhost:15672` / `vendingad` / `vendingad@123`
- Seq: `http://localhost:5341`

## RabbitMQ / Event Publishing

Config in `appsettings.json`:

```json
"RabbitMQ": {
  "Enabled": false,
  "HostName": "localhost",
  "Port": 5672,
  "UserName": "vendingad",
  "Password": "vendingad@123",
  "ExchangeName": "vendingad.events",
  "ScheduleChangedQueueName": "vendingad.worker.schedule-changed"
}
```

Meaning:

- `IMessagePublisher` is the abstraction for publishing integration events.
- `NullMessagePublisher` is used when RabbitMQ is disabled.
- `RabbitMqMessagePublisher` publishes JSON messages to topic exchange `vendingad.events` when enabled.
- Current event contracts: `ScheduleChangedEvent`, `VideoUploadedEvent`.
- Schedule create/update/delete/toggle/item reorder flows save DB changes first, then publish `ScheduleChangedEvent`.
- Web requests no longer directly warm or invalidate schedule cache.
- `ScheduleChangedEvent.AffectedDeviceCodes` means all devices affected before or after the change.
- `VendingAdWorker` consumes `ScheduleChangedEvent` from queue `vendingad.worker.schedule-changed` with routing key `schedule.changed`.
- Worker invalidates per-device playback cache keys and warms schedule content cache for active schedules.
- Worker requires Redis through `AddWorkerInfrastructure`; startup fails fast if Redis is disabled or missing.
- Worker validates database, Redis, and RabbitMQ connectivity before consuming messages.
- Worker logs handler failures and acknowledges messages for this milestone; stale cache is bounded by TTL fallback.

Run app with RabbitMQ and Redis enabled temporarily:

```bash
Redis__Enabled=true \
RabbitMQ__Enabled=true \
dotnet run --no-launch-profile --project "VendingAdSolution/VendingAdSystem"
```

Run worker locally:

```bash
dotnet run --project "VendingAdSolution/VendingAdWorker"
```

Run app against local SQL Server:

```bash
DatabaseProvider=SqlServer \
ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=VendingAdDb;User Id=sa;Password=VendingAd@12345;TrustServerCertificate=True;" \
Database__ApplyMigrationsOnStartup=true \
Database__EnsureCreatedOnStartup=false \
Seed__EnableDemoData=true \
Redis__Enabled=true \
RabbitMQ__Enabled=true \
dotnet run --no-launch-profile --project "VendingAdSolution/VendingAdSystem"
```

Run worker against local SQL Server:

```bash
DatabaseProvider=SqlServer \
ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=VendingAdDb;User Id=sa;Password=VendingAd@12345;TrustServerCertificate=True;" \
Redis__Enabled=true \
Redis__ConnectionString=localhost:6379 \
dotnet run --project "VendingAdSolution/VendingAdWorker"
```

Run app against SQL Server in Codespaces/local:

```bash
DatabaseProvider=SqlServer \
ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=VendingAdDb;User Id=sa;Password=VendingAd@12345;TrustServerCertificate=True;" \
dotnet run --no-launch-profile --project "VendingAdSolution/VendingAdSystem"
```

Apply SQL Server migrations manually:

```bash
DatabaseProvider=SqlServer \
ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=VendingAdDb;User Id=sa;Password=VendingAd@12345;TrustServerCertificate=True;" \
dotnet ef database update --project "VendingAdSolution/VendingAdSystem"
```

If `dotnet ef` is not installed, install it locally or globally:

```bash
dotnet tool install dotnet-ef --version 8.0.0 --global
```

Current cache keys:

- `mobile:playback-state:{deviceCode}`
- `mobile:device-active-schedule:{deviceCode}`
- `mobile:schedule-content:{scheduleId}:{version}`
- `lock:mobile:schedule-content:{scheduleId}:{version}`
- `device:online:{deviceCode}`

Meaning:

- Per-device response cache speeds up repeated polling for the same device.
- Shared schedule cache lets many devices reuse one schedule payload instead of loading the same ordered media list from DB many times.
- Redis lock reduces cache stampede when many requests miss the same shared cache simultaneously.
- Device presence key tracks online/offline state with TTL and reduces heartbeat DB writes.

## Health Checks

Web endpoints:

- `GET /health/live`
- `GET /health/ready`

Meaning:

- `/health/live` checks that the web process is running.
- `/health/ready` checks database connectivity.
- `/health/ready` checks Redis only when `Redis:Enabled=true`.
- `/health/ready` checks RabbitMQ only when `RabbitMQ:Enabled=true`.
- Redis/RabbitMQ disabled by config are reported as healthy for local quick-start mode.

Useful checks:

```bash
curl http://localhost:8080/health/live
curl http://localhost:8080/health/ready
```

Worker readiness:

- Worker has no HTTP endpoint yet.
- Worker validates DB, Redis, and RabbitMQ during startup.
- If one dependency is unavailable, worker startup fails before consuming messages.

## Milestone 9.5 E2E Verification

Start local infrastructure:

```bash
docker compose -f docker-compose.infra.yml up -d redis rabbitmq
```

Run web with event publishing and Redis enabled:

```bash
Redis__Enabled=true \
RabbitMQ__Enabled=true \
dotnet run --no-launch-profile --project "VendingAdSolution/VendingAdSystem"
```

Run worker:

```bash
dotnet run --project "VendingAdSolution/VendingAdWorker"
```

Manual verification flow:

1. Open CMS and login as `test@test` / `test@test`.
2. Create or edit a playback schedule assigned to at least one device.
3. Confirm web publishes `ScheduleChangedEvent`.
4. Confirm worker logs `Consumed ScheduleChangedEvent`.
5. Confirm RabbitMQ queue returns to zero ready messages:

```bash
docker exec vendingad-rabbitmq rabbitmqctl list_queues name messages_ready messages_unacknowledged
```

6. Confirm Redis contains or removes expected mobile cache keys:

```bash
docker exec vendingad-redis redis-cli --scan --pattern 'mobile:*'
```

## Device Presence / Heartbeat

Config in `appsettings.json`:

```json
"DevicePresence": {
  "OnlineTtlSeconds": 90,
  "DbWriteIntervalSeconds": 60
}
```

Meaning:

- Heartbeat sets `device:online:{deviceCode}` with TTL.
- Device is considered online while the key exists; DB `LastSeen` is used as fallback.
- DB `LastSeen` is updated only after the configured interval, not on every heartbeat.
- Dashboard online/offline counts now prefer presence service instead of raw `LastSeen < 5 minutes` checks.

## Mobile API Rate Limiting

Config in `appsettings.json`:

```json
"MobileRateLimiting": {
  "WindowSeconds": 60,
  "HeartbeatPermitLimit": 10,
  "PlaybackStatePermitLimit": 30
}
```

Meaning:

- `POST /api/mobile/heartbeat` is limited per `deviceCode`.
- `GET /api/mobile/playback-state/{deviceCode}` is limited per `deviceCode`.
- When the limit is exceeded, API returns HTTP `429 Too Many Requests` with `Retry-After`.
- Current limiter is in-process and protects a single backend instance; a future distributed limiter can move counters to Redis if the app runs multiple web instances.

## Current UI Notes

- Profile and Settings are placeholders showing `Sẽ cập nhật tính năng sau.`
- Video pages use fallback thumbnail asset: `wwwroot/images/video-placeholder.svg`
- Real thumbnail generation is planned for a later Worker/FFmpeg milestone.
- CMS visual language should remain light, simple, consistent, and blue-based.
- Avoid colorful gradients, emoji icons as main UI elements, and marketing-style redesigns.

## Completed Milestones

See `MILESTONES.md` for full details.

Main completed areas so far:

- Mobile/TV box API foundation
- Database indexing
- EF Core read query optimization (`AsNoTracking()`)
- Redis playback-state cache
- Shared schedule playback cache for multi-device schedules
- Redis device presence / heartbeat write throttling
- Mobile API rate limiting for heartbeat and playback-state
- Event-driven schedule cache invalidation through RabbitMQ worker
- Health checks and E2E verification for DB/Redis/RabbitMQ/worker flow
- UI/UX improvements for date/time, thumbnails, video and playlist pages

## Recommended Next Steps

1. Clone and run the project locally with SQL Server to verify the new setup outside Codespaces.
2. Video metadata / thumbnail pipeline.
3. Object storage / CDN.
4. Observability and structured logs.
5. Load testing with simulated devices.
6. Transactional outbox / DLQ when message reliability becomes production-critical.

## Useful Commands

Build:

```bash
dotnet build "VendingAdSolution/VendingAdSolution.sln"
```

Test:

```bash
dotnet test "VendingAdSolution/VendingAdSolution.sln"
```

Run app locally:

```bash
dotnet run --no-launch-profile --project "VendingAdSolution/VendingAdSystem"
```

Run app with Redis enabled temporarily:

```bash
Redis__Enabled=true dotnet run --no-launch-profile --project "VendingAdSolution/VendingAdSystem"
```
