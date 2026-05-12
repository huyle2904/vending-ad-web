# Production Learning Roadmap

## Goal

Use this project as a practical production-readiness case study while learning technologies that are valuable for real backend systems and CV building.

Primary goals:

- Prepare the web app and mobile/TV box APIs for 100-200 active devices.
- Reduce database load from high-frequency device polling and heartbeat traffic.
- Move heavy media work out of synchronous HTTP requests.
- Learn and apply Redis, RabbitMQ, indexing, background workers, object storage, observability, and load testing.
- Document the implementation clearly for portfolio/CV use.

## Target Architecture

```text
Web CMS / Mobile App / TV Box App
        |
        v
ASP.NET Core Web API
        |
        |-- PostgreSQL: durable relational data
        |-- Redis: cache, device presence, rate limit state
        |-- RabbitMQ: asynchronous events and jobs
        |
        v
.NET Worker Service
        |
        |-- Object Storage/CDN: video files
        |-- PostgreSQL: metadata/status updates
        |-- Redis: cache invalidation and runtime state
```

## Milestone Tracker

| # | Milestone | Main Tech | Status |
|---|-----------|-----------|--------|
| 1 | Database indexing | EF Core, PostgreSQL | Done |
| 2 | Mobile query optimization | EF Core | Done |
| 2B | Portal/admin read query optimization | EF Core | Done |
| 3 | Redis playback cache | Redis, StackExchange.Redis | Planned |
| 4 | Redis device presence | Redis TTL | Planned |
| 5 | Mobile API rate limiting | ASP.NET Core RateLimiter | Planned |
| 6 | RabbitMQ infrastructure | RabbitMQ, Docker | Planned |
| 7 | Worker service | .NET Worker Service | Planned |
| 8 | Schedule cache invalidation | RabbitMQ, Redis | Planned |
| 9 | Video metadata pipeline | RabbitMQ, Worker, FFmpeg/Checksum | Planned |
| 10 | Object storage/CDN | S3/R2, CDN | Planned |
| 11 | Observability | Serilog, Health Checks, Seq/Sentry | Planned |
| 12 | Local infrastructure | Docker Compose | Planned |
| 13 | Load testing | k6 or NBomber | Planned |
| 14 | CV/README documentation | Markdown, diagrams | Planned |

Status values: `Planned`, `In Progress`, `Done`, `Skipped`.

---

## Milestone 1: Database Indexing

### Problem

Mobile playback APIs and CMS pages frequently query by device code, user ID, schedule date range, active status, and schedule-device relationships. Without indexes, these queries can become slow as data grows.

### Solution

Add indexes in `AppDbContext.OnModelCreating` for high-frequency lookup paths.

### Implementation Scope

- `Devices.DeviceCode` unique index.
- `Devices.UserId`.
- `Devices.IsActive`.
- `PlaybackSchedules.UserId`.
- `PlaybackSchedules.IsActive`.
- `PlaybackSchedules.StartDate`.
- `PlaybackSchedules.EndDate`.
- `PlaybackSchedules.CreatedAt`.
- Composite index: `PlaybackSchedules(IsActive, StartDate, EndDate)`.
- Composite index: `PlaybackSchedules(UserId, IsActive, CreatedAt)`.
- `PlaybackScheduleDevices.DeviceId`.
- `PlaybackScheduleDevices.PlaybackScheduleId`.
- Composite index: `PlaybackScheduleDevices(DeviceId, PlaybackScheduleId)`.
- `PlaybackScheduleItems.PlaybackScheduleId`.
- Composite index: `PlaybackScheduleItems(PlaybackScheduleId, OrderIndex)`.
- `Medias.UserId`.
- `Medias.UploadedAt`.

### Learning Outcome

- Understand relational index design.
- Learn how EF Core maps indexes for multi-provider databases.
- Learn the difference between single-column and composite indexes.

### CV Highlight

> Designed database indexes for high-frequency IoT device playback queries using EF Core and PostgreSQL.

### Status

Done

### Implementation Notes

- Added EF Core indexes in `Infrastructure/Persistence/AppDbContext.cs` for device lookup, schedule date filtering, schedule-device joins, ordered schedule items, and media/user queries.
- Added `Devices.ClaimCode` index as an extra optimization because the claim flow validates claim codes during device assignment.
- These model indexes are automatically created for new databases. Existing databases may need EF Core migrations or provider-specific index repair scripts to apply them.

---

## Milestone 2: Mobile Query Optimization

### Problem

Mobile APIs are read-heavy and may load more EF entities than needed. Tracking read-only entities adds unnecessary overhead.

### Solution

Optimize mobile API queries with `AsNoTracking()`, focused includes, and projection where appropriate.

### Implementation Scope

- Update `MobilePlaybackService.GetDeviceAsync` to use `AsNoTracking()`.
- Update `MobilePlaybackService.GetPlaybackStateAsync` to use `AsNoTracking()` for read-only schedule queries.
- Review whether schedule lookup can query from `PlaybackScheduleDevices` directly.
- Keep `HeartbeatAsync` tracked because it updates `LastSeen`.

### Learning Outcome

- Learn EF Core tracking vs no-tracking queries.
- Learn projection-based optimization.
- Understand query shape and payload size.

### CV Highlight

> Optimized EF Core read paths with no-tracking queries for high-frequency mobile API endpoints.

### Status

Done

### Implementation Notes

- Added `AsNoTracking()` to read-only mobile queries in `Application/Services/MobilePlaybackService.cs`.
- Kept `HeartbeatAsync` tracked because it updates `Device.LastSeen` and calls `SaveChangesAsync()`.
- This milestone covers mobile API read paths first because device polling is the highest-frequency workload.

---

## Milestone 2B: Portal/Admin Read Query Optimization

### Problem

CMS and admin list/dashboard pages load many entities only to render views. EF Core tracking is unnecessary for these read-only queries and adds avoidable memory/CPU overhead.

### Solution

Apply `AsNoTracking()` to portal/admin/API/service queries that only read data for display or response generation. Keep tracking enabled for update/delete flows.

### Implementation Scope

- Portal dashboard, video, device, playlist, and schedule list queries.
- Admin dashboard, devices, videos, playlists, schedules, and users list queries.
- Portal API device list query.
- Read-only schedule and playlist service methods.
- Avoid POST update/delete/assign flows because those need tracked entities for `SaveChangesAsync()`.

### Learning Outcome

- Practice classifying queries as read-only or write-flow queries.
- Understand why no-tracking is safe for view rendering but unsafe before entity updates/deletes.
- Learn to optimize EF Core gradually without changing business behavior.

### CV Highlight

> Optimized portal and admin read-only EF Core queries with no-tracking behavior to reduce request memory overhead.

### Status

Done

### Implementation Notes

- Added `AsNoTracking()` to safe read-only queries in `PortalController`, `AdminController`, `PortalApiController`, `PlaybackScheduleService`, and `PlaylistManagementService`.
- Intentionally kept tracking for heartbeat, delete, update, toggle, assign, and reorder flows.

---

## Milestone 3: Redis Playback Cache

### Problem

100-200 devices polling `/api/mobile/playback-state/{deviceCode}` can repeatedly hit PostgreSQL even when schedule data has not changed.

### Solution

Use Redis with a cache-aside pattern for playback-state responses.

### Implementation Scope

- Add Redis config:

```json
{
  "Redis": {
    "Enabled": true,
    "ConnectionString": "localhost:6379"
  }
}
```

- Add cache abstraction:
  - `ICacheService`
  - `RedisCacheService`
  - optional `NullCacheService` fallback
- Cache key: `mobile:playback-state:{deviceCode}`.
- Suggested TTL:
  - active schedule: 5 seconds
  - no active schedule: 10 seconds
  - unclaimed device: 10-30 seconds
- Log cache hit/miss.

### Learning Outcome

- Learn Redis basics.
- Learn distributed caching and TTL.
- Learn cache-aside pattern.

### CV Highlight

> Implemented Redis cache-aside strategy for high-frequency mobile playback APIs, reducing database pressure with TTL-based caching.

### Status

Planned

---

## Milestone 4: Redis Device Presence

### Problem

Heartbeat writes can overload the database if every device updates `LastSeen` frequently.

### Solution

Use Redis TTL keys for realtime online/offline presence and throttle durable DB writes.

### Implementation Scope

- On heartbeat, set Redis key: `device:online:{deviceCode}`.
- TTL: 3-5 minutes.
- Update database `LastSeen` only if the previous value is older than 30-60 seconds.
- Keep DB `LastSeen` for durable history.
- Optional later: dashboard online/offline reads from Redis.

### Learning Outcome

- Learn Redis TTL as presence tracking.
- Learn write throttling.
- Understand realtime state vs durable state.

### CV Highlight

> Built Redis TTL-based presence tracking for IoT-style device heartbeat and reduced persistent database writes.

### Status

Planned

---

## Milestone 5: Mobile API Rate Limiting

### Problem

Buggy device clients or retry storms can overload mobile APIs.

### Solution

Add endpoint-specific rate limiting for mobile APIs.

### Implementation Scope

- Use ASP.NET Core built-in RateLimiter.
- Protect:
  - `/api/mobile/heartbeat`
  - `/api/mobile/playback-state/{deviceCode}`
  - `/api/mobile/devices/{deviceCode}`
- Suggested policies:
  - heartbeat: 10 requests/minute/device
  - playback-state: 20 requests/minute/device
  - device info: 20 requests/minute/device
- Prefer partition key by `deviceCode`, not only IP.
- Start with in-memory limiter; later consider Redis-backed distributed limiter.

### Learning Outcome

- Learn API protection strategies.
- Learn fixed-window/sliding-window/token-bucket concepts.
- Understand NAT/IP limitations for device fleets.

### CV Highlight

> Implemented endpoint-specific rate limiting to protect mobile device APIs from retry storms and misbehaving clients.

### Status

Planned

---

## Milestone 6: RabbitMQ Infrastructure

### Problem

Some work should not happen synchronously during HTTP requests, such as media metadata processing and schedule-related cache invalidation.

### Solution

Introduce RabbitMQ for asynchronous event-driven processing.

### Implementation Scope

- Add RabbitMQ to local Docker Compose.
- Add message publisher abstraction:
  - `IMessagePublisher`
  - `RabbitMqMessagePublisher`
- Define event contracts:
  - `VideoUploadedEvent`
  - `ScheduleChangedEvent`
- Initial queues:
  - `video.uploaded`
  - `schedule.changed`
  - dead-letter queue later

### Learning Outcome

- Learn queues, exchanges, routing keys, durable messages.
- Learn event-driven backend design.
- Learn failure and retry concepts.

### CV Highlight

> Added RabbitMQ-based event infrastructure for asynchronous backend workflows.

### Status

Planned

---

## Milestone 7: Worker Service

### Problem

Background jobs should be independently scalable and isolated from web request handling.

### Solution

Create a .NET Worker Service to consume RabbitMQ events.

### Implementation Scope

- Add project: `VendingAdWorker`.
- Optionally add shared contracts project later.
- Worker responsibilities:
  - consume queue messages
  - process jobs
  - update PostgreSQL
  - invalidate Redis cache
  - log failures

### Learning Outcome

- Learn .NET Worker Service.
- Learn service separation.
- Learn long-running process deployment.

### CV Highlight

> Separated web API and background worker services for scalable asynchronous processing.

### Status

Planned

---

## Milestone 8: Schedule Cache Invalidation

### Problem

Redis playback cache may serve stale schedule data until TTL expires.

### Solution

Publish `ScheduleChangedEvent` when schedules change, then worker invalidates Redis cache for affected devices.

### Implementation Scope

- Publish event after:
  - create schedule
  - create immediate schedule
  - update schedule
  - toggle schedule
  - delete schedule
  - add/remove/reorder schedule items
- Event fields:
  - `ScheduleId`
  - `UserId`
  - `DeviceCodes`
  - `ChangedAtUtc`
  - `ChangeType`
- Worker deletes keys:
  - `mobile:playback-state:{deviceCode}`

### Learning Outcome

- Learn event-driven cache invalidation.
- Understand consistency tradeoffs between TTL and invalidation.

### CV Highlight

> Implemented event-driven Redis cache invalidation for device playback schedules using RabbitMQ.

### Status

Planned

---

## Milestone 9: Video Metadata Pipeline

### Problem

Video metadata extraction can be slow and should not block upload requests.

### Solution

Publish `VideoUploadedEvent` after upload and process metadata asynchronously in the worker.

### Implementation Scope

- Add fields to `Media` when needed:
  - `Checksum`
  - `ProcessingStatus`
  - `ProcessedAt`
  - optional `ContentType`
- Worker tasks:
  - calculate SHA256 checksum
  - extract duration with FFmpeg/ffprobe later
  - update media metadata
- Start small with checksum/status, then add duration.

### Learning Outcome

- Learn background media processing.
- Learn idempotent job processing.
- Learn checksum validation.

### CV Highlight

> Built asynchronous video metadata processing with RabbitMQ and .NET Worker Service.

### Status

Planned

---

## Milestone 10: Object Storage And CDN

### Problem

ASP.NET web servers should not serve large video files to many devices. Local uploads can be lost or inconsistent across deployments/instances.

### Solution

Move video files to object storage and deliver them through CDN/object storage URLs.

### Implementation Scope

- Add interface:

```csharp
public interface IFileStorageService
{
    Task<FileUploadResult> UploadAsync(Stream stream, string fileName, string contentType);
    Task DeleteAsync(string objectKey);
}
```

- Implement providers:
  - `LocalFileStorageService`
  - `S3CompatibleFileStorageService`
- Config switch:
  - `Storage:Provider = Local | S3 | R2`
- Recommended providers:
  - Cloudflare R2 for low-cost personal production
  - AWS S3 + CloudFront for enterprise-style CV value

### Learning Outcome

- Learn object storage.
- Learn CDN-backed file delivery.
- Learn separating binary storage from app runtime.

### CV Highlight

> Integrated S3-compatible object storage and CDN-backed media delivery for scalable device video downloads.

### Status

Planned

---

## Milestone 11: Observability

### Problem

When production issues happen, logs and health checks are needed to locate bottlenecks across API, DB, Redis, RabbitMQ, and storage.

### Solution

Add structured logging, health checks, and optional tracing/metrics.

### Implementation Scope

- Add Serilog.
- Add health endpoints:
  - `/health/live`
  - `/health/ready`
- Health checks:
  - PostgreSQL
  - Redis
  - RabbitMQ
  - storage config later
- Optional tools:
  - Seq for local structured logs
  - Sentry for production exceptions
  - OpenTelemetry later
- Log useful fields:
  - `deviceCode`
  - `scheduleId`
  - `mediaId`
  - `userId`
  - `cacheHit`
  - `queueName`
  - `durationMs`

### Learning Outcome

- Learn production debugging.
- Learn structured logs and dependency health.
- Understand readiness vs liveness checks.

### CV Highlight

> Added production observability with structured logging, health checks, and dependency monitoring.

### Status

Planned

---

## Milestone 12: Local Infrastructure With Docker Compose

### Problem

Redis, RabbitMQ, PostgreSQL, and logging tools should be easy to run locally for learning and testing.

### Solution

Add Docker Compose for local infrastructure.

### Implementation Scope

Suggested services:

```text
postgres
redis
rabbitmq
seq
minio optional
```

RabbitMQ management UI should be enabled for learning.

### Learning Outcome

- Learn Docker Compose.
- Learn local production-like infrastructure.
- Learn service configuration via environment variables.

### CV Highlight

> Containerized local development infrastructure with Docker Compose for PostgreSQL, Redis, RabbitMQ, and observability tools.

### Status

Planned

---

## Milestone 13: Load Testing

### Problem

Optimizations should be validated with repeatable load tests.

### Solution

Add load test scripts that simulate 100-200 mobile/TV box devices.

### Implementation Scope

- Choose k6 or NBomber.
- Suggested first choice: k6.
- Add scripts:
  - `loadtests/mobile-playback.js`
  - `loadtests/heartbeat.js`
- Scenarios:
  - 200 devices heartbeat every 30 seconds
  - 200 devices playback-state every 15 seconds
  - random jitter
  - check latency/error rate
- Track:
  - average latency
  - p95 latency
  - p99 latency
  - error rate
  - Redis hit rate
  - DB pressure
  - RabbitMQ queue depth

### Learning Outcome

- Learn practical load testing.
- Learn latency percentiles.
- Learn how to prove backend improvements.

### CV Highlight

> Load tested mobile playback APIs with k6, simulating 200 concurrent IoT devices and measuring p95 latency.

### Status

Planned

---

## Milestone 14: CV And README Documentation

### Problem

Good engineering work should be visible and understandable to reviewers, recruiters, and future maintainers.

### Solution

Document the architecture, problems solved, technologies used, and measured outcomes.

### Implementation Scope

- Update README with architecture overview.
- Add architecture diagram.
- Document Redis/RabbitMQ flows.
- Document load test results.
- Add production notes and tradeoffs.

### Learning Outcome

- Learn technical documentation.
- Learn how to communicate architecture decisions.

### CV Highlight

> Documented production architecture, performance optimizations, and load testing results for a real ASP.NET Core device playback platform.

### Status

Planned

---

## Suggested CV Summary

```text
Vending Advertisement Platform - ASP.NET Core, PostgreSQL, Redis, RabbitMQ

- Built a production-ready video scheduling platform for vending machine displays and TV box devices.
- Designed mobile playback APIs with server-time synchronization, schedule versioning, and offline-first media download support.
- Optimized high-frequency device polling using Redis cache-aside strategy and TTL-based device presence tracking.
- Implemented RabbitMQ-based event-driven background processing for schedule cache invalidation and media metadata jobs.
- Added PostgreSQL indexing and EF Core query optimization for schedule/device lookup performance.
- Integrated S3-compatible object storage for scalable CDN-backed video delivery.
- Added health checks, structured logging, and load testing scripts simulating 200 concurrent devices.
```

## Recommended Execution Order

1. Database indexing.
2. Mobile query optimization.
3. Redis playback cache.
4. Redis heartbeat presence.
5. Rate limiting.
6. RabbitMQ infrastructure.
7. Worker service.
8. Schedule cache invalidation.
9. Video metadata pipeline.
10. Object storage/CDN.
11. Observability.
12. Docker Compose local infrastructure.
13. Load testing.
14. README/CV documentation.

## Notes

- Keep each milestone small and commit separately.
- Prefer production-realistic implementation over adding technology without purpose.
- Update this file after each milestone with status and lessons learned.
- Keep visible UI text in Vietnamese, but keep internal code identifiers in English.
