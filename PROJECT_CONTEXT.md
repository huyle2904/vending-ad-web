# Project Context

## Overview

- Repository: `/workspaces/vending-ad`
- Solution: `VendingAdSolution/VendingAdSolution.sln`
- Main app: `VendingAdSolution/VendingAdSystem/VendingAdSystem.csproj`
- Worker app: `VendingAdSolution/VendingAdWorker/VendingAdWorker.csproj`
- Shared contracts: `VendingAdSolution/VendingAd.Contracts/VendingAd.Contracts.csproj`
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
- Services: `VendingAdSolution/VendingAdSystem/Application/Services/`
- DTOs: `VendingAdSolution/VendingAdSystem/Application/DTOs/`
- Entities: `VendingAdSolution/VendingAdSystem/Domain/Entities/`
- EF context: `VendingAdSolution/VendingAdSystem/Infrastructure/Persistence/AppDbContext.cs`
- DI: `VendingAdSolution/VendingAdSystem/Infrastructure/DependencyInjection.cs`
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
- Startup uses `Database.Migrate()` for SQL Server and keeps `EnsureCreated()` for SQLite quick-dev mode.

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
  "ExchangeName": "vendingad.events"
}
```

Meaning:

- `IMessagePublisher` is the abstraction for publishing integration events.
- `NullMessagePublisher` is used when RabbitMQ is disabled.
- `RabbitMqMessagePublisher` publishes JSON messages to topic exchange `vendingad.events` when enabled.
- Current event contracts: `ScheduleChangedEvent`, `VideoUploadedEvent`.
- Schedule create/update/delete/toggle/item reorder flows publish `ScheduleChangedEvent` after DB/cache work.
- `VendingAdWorker` consumes `ScheduleChangedEvent` from queue `vendingad.worker.schedule-changed` with routing key `schedule.changed`.
- Current worker behavior logs consumed events and acknowledges messages; real cache handling is planned for the next milestone.

Run app with RabbitMQ enabled temporarily:

```bash
RabbitMQ__Enabled=true dotnet run --no-launch-profile --project "VendingAdSolution/VendingAdSystem"
```

Run worker locally:

```bash
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
- UI/UX improvements for date/time, thumbnails, video and playlist pages

## Recommended Next Steps

1. RabbitMQ infrastructure.
2. Worker service.
3. Event-driven cache invalidation.
4. Video metadata / thumbnail pipeline.
5. Object storage / CDN.
6. Observability.
7. Load testing.

## Useful Commands

Build:

```bash
dotnet build "VendingAdSolution/VendingAdSolution.sln"
```

Run app locally:

```bash
dotnet run --no-launch-profile --project "VendingAdSolution/VendingAdSystem"
```

Run app with Redis enabled temporarily:

```bash
Redis__Enabled=true dotnet run --no-launch-profile --project "VendingAdSolution/VendingAdSystem"
```
