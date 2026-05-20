# VendingAd Local Setup

This solution contains:

- `VendingAdSystem`: ASP.NET Core MVC/API web app
- `VendingAdWorker`: background worker for RabbitMQ events
- `VendingAd.Application`: DTOs, contracts, business services
- `VendingAd.Domain`: entities
- `VendingAd.Infrastructure`: EF Core, cache, RabbitMQ, health checks
- `VendingAd.Contracts`: integration event contracts

## Prerequisites

- .NET 8 SDK
- Docker Desktop, recommended for SQL Server, Redis, and RabbitMQ
- FFmpeg/ffprobe, recommended for production-like video validation
- Visual Studio 2022 or VS Code

## Restore, Build, Test

```powershell
dotnet restore VendingAdSolution.sln
dotnet build VendingAdSolution.sln
dotnet test VendingAdSolution.sln
```

## Start Local Infrastructure

From the repository root:

```powershell
docker compose -f docker-compose.infra.yml up -d sqlserver redis rabbitmq
```

Local service defaults:

- SQL Server: `localhost,1433`
- SQL Server user: `sa`
- SQL Server password: `VendingAd@12345`
- Redis: `localhost:6379`
- RabbitMQ: `localhost:5672`
- RabbitMQ management UI: `http://localhost:15672`
- RabbitMQ user/password: `vendingad` / `vendingad@123`

## Run Web With SQL Server

From the repository root:

```powershell
$env:ASPNETCORE_ENVIRONMENT="Development"
$env:DatabaseProvider="SqlServer"
$env:ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=VendingAdDb;User Id=sa;Password=VendingAd@12345;TrustServerCertificate=True;"
$env:Database__ApplyMigrationsOnStartup="true"
$env:Database__EnsureCreatedOnStartup="false"
$env:Database__ResetOnStartup="false"
$env:Seed__EnableDemoData="true"
$env:Redis__Enabled="true"
$env:RabbitMQ__Enabled="true"
$env:RabbitMQ__UserName="vendingad"
$env:RabbitMQ__Password="vendingad@123"
$env:VideoValidation__FfprobeEnabled="true"
$env:VideoValidation__RequireFfprobe="false"

dotnet run --no-launch-profile --project VendingAdSolution/VendingAdSystem
```

Open:

- Web app: `http://localhost:8080`
- Health live: `http://localhost:8080/health/live`
- Health ready: `http://localhost:8080/health/ready`

Seeded accounts when `Seed__EnableDemoData=true`:

- Admin: `admin@admin` / `admin@admin`
- Demo user: `test@test` / `test@test`

Seeded demo device secrets:

- `TAB-01`: `dev-secret-TAB-01`
- `TAB-02`: `dev-secret-TAB-02`
- `CLAIM-TEST-290403`: `dev-secret-CLAIM-TEST-290403`
- `CLAIM-TEST-210603`: `dev-secret-CLAIM-TEST-210603`

Mobile/device API calls must send either `X-Device-Secret: <secret>` or `Authorization: Bearer <secret>`.

Admin can rotate or revoke a device secret from `/admin/devices`. A rotated secret is shown once and the old secret stops working immediately.

## Run Worker

Open a second terminal:

```powershell
$env:DOTNET_ENVIRONMENT="Development"
$env:DatabaseProvider="SqlServer"
$env:ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=VendingAdDb;User Id=sa;Password=VendingAd@12345;TrustServerCertificate=True;"
$env:Redis__Enabled="true"
$env:Redis__ConnectionString="localhost:6379"
$env:RabbitMQ__UserName="vendingad"
$env:RabbitMQ__Password="vendingad@123"

dotnet run --project VendingAdSolution/VendingAdWorker
```

The worker validates database, Redis, and RabbitMQ connectivity during startup.

## Optional: Use Example Appsettings

Example config files are available:

- `VendingAdSystem/appsettings.Development.example.json`
- `VendingAdWorker/appsettings.Development.example.json`

Copy the relevant example to `appsettings.Development.json` only for local work, then adjust credentials if needed.

## Event-Driven Cache Verification

1. Start SQL Server, Redis, and RabbitMQ.
2. Run the web app with `RabbitMQ__Enabled=true` and `Redis__Enabled=true`.
3. Run the worker.
4. Login to the CMS as `test@test`.
5. Create, edit, toggle, or delete a playback schedule.
6. Confirm the worker logs `Consumed ScheduleChangedEvent`.
7. Check RabbitMQ queue depth:

```powershell
docker exec vendingad-rabbitmq rabbitmqctl list_queues name messages_ready messages_unacknowledged
```

8. Check Redis mobile cache keys:

```powershell
docker exec vendingad-redis redis-cli --scan --pattern 'mobile:*'
```

## Startup Flags

`Database:ApplyMigrationsOnStartup`

- `true`: web app runs EF Core migrations for relational databases during startup (SQL Server/PostgreSQL/SQLite).
- Good for local/dev.
- Consider `false` for production and run migrations in deployment.

`Database:EnsureCreatedOnStartup`

- `true`: web app calls `EnsureCreated()` for quick SQLite startup.
- Do not use for SQL Server migration-based environments.

`Database:ResetOnStartup`

- `true`: web app calls `EnsureDeleted()` first, then recreates schema using migrations or `EnsureCreated`.
- Intended for disposable/test databases only.
- Keep `false` in normal environments.

`Database:ResetSchemaOnStartup`

- `true`: drops PostgreSQL public tables, including EF migration history, then reruns migrations.
- Intended for disposable/test databases only.
- Keep `false` in normal environments.

`Seed:EnableDemoData`

- `true`: seeds demo/admin accounts and sample data.
- Good for local/dev.
- Defaults to `false` in committed production config.
- Startup fails outside `Development` unless `Seed:AllowDemoDataOutsideDevelopment=true`.

`Seed:AllowDemoDataOutsideDevelopment`

- `true`: allows demo/admin account seeding in non-Development environments.
- Intended only for disposable demo environments such as the current Render test deployment.

`VideoValidation:FfprobeEnabled`

- `true`: uploaded videos are probed with `ffprobe` after basic extension/MIME/magic-byte checks.
- If `ffprobe` is present and rejects the file, upload fails.
- `VideoValidation:RequireFfprobe=true` makes uploads fail closed when `ffprobe` is missing.
- `VideoValidation:AllowedVideoCodecs` defaults to `h264`, `hevc`, `vp8`, `vp9`, and `av1`.

## Render Quick Recovery (Disposable DB)

If Render login works but dashboard returns HTTP 500 after schema changes, reset and recreate the temporary PostgreSQL DB:

1. Set env vars in Render:
   - `DatabaseProvider=Postgres`
   - `Database__ResetOnStartup=true`
   - `Database__ApplyMigrationsOnStartup=true`
   - `Database__EnsureCreatedOnStartup=false`
   - `Seed__EnableDemoData=true`
   - `Seed__AllowDemoDataOutsideDevelopment=true`
2. Redeploy once.
3. After successful boot, set `Database__ResetOnStartup=false` and redeploy again.
