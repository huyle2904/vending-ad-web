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
$env:DatabaseProvider="SqlServer"
$env:ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=VendingAdDb;User Id=sa;Password=VendingAd@12345;TrustServerCertificate=True;"
$env:Database__ApplyMigrationsOnStartup="true"
$env:Database__EnsureCreatedOnStartup="false"
$env:Seed__EnableDemoData="true"
$env:Redis__Enabled="true"
$env:RabbitMQ__Enabled="true"

dotnet run --no-launch-profile --project VendingAdSolution/VendingAdSystem
```

Open:

- Web app: `http://localhost:8080`
- Health live: `http://localhost:8080/health/live`
- Health ready: `http://localhost:8080/health/ready`

Seeded accounts when `Seed__EnableDemoData=true`:

- Admin: `admin@admin` / `admin@admin`
- Demo user: `test@test` / `test@test`

## Run Worker

Open a second terminal:

```powershell
$env:DatabaseProvider="SqlServer"
$env:ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=VendingAdDb;User Id=sa;Password=VendingAd@12345;TrustServerCertificate=True;"
$env:Redis__Enabled="true"
$env:Redis__ConnectionString="localhost:6379"

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

- `true`: web app runs EF Core migrations for SQL Server during startup.
- Good for local/dev.
- Consider `false` for production and run migrations in deployment.

`Database:EnsureCreatedOnStartup`

- `true`: web app calls `EnsureCreated()` for quick SQLite startup.
- Do not use for SQL Server migration-based environments.

`Seed:EnableDemoData`

- `true`: seeds demo/admin accounts and sample data.
- Good for local/dev.
- Use `false` for real production data.
