# VendingAd Local Setup

The solution contains the ASP.NET Core CMS and API, an optional background worker, application and domain projects, infrastructure integrations, and tests.

## Prerequisites

- .NET SDK 8.x.
- Docker with Docker Compose for local infrastructure.
- FFmpeg and ffprobe for production-equivalent video validation.
- Node.js 22 when running Playwright.

## Configure local secrets

From the repository root, copy `.env.example` to `.env` and replace every placeholder.
Then copy these files and replace their placeholders:

- `VendingAdSystem/appsettings.Development.example.json` to `VendingAdSystem/appsettings.Development.json`.
- `VendingAdWorker/appsettings.Development.example.json` to `VendingAdWorker/appsettings.Development.json`.

Both destination files and `.env` are ignored by Git.
Do not place production credentials in any repository file.

## Start infrastructure

From the repository root:

```powershell
docker compose -f docker-compose.infra.yml up -d postgres redis rabbitmq
```

SQL Server, Seq, Prometheus, and Grafana are optional services in the same Compose file.

## Restore, build, and migrate

```powershell
dotnet restore VendingAdSolution/VendingAdSolution.sln
dotnet build VendingAdSolution/VendingAdSolution.sln --configuration Release
dotnet run --project VendingAdSolution/VendingAdSystem -- --migrate
```

Production runs the same `--migrate` command as a separate pre-deploy step.
Normal production startup must keep `Database__ApplyMigrationsOnStartup=false`.

## Bootstrap the first admin

The database no longer contains a built-in admin password.
After migrations and only when no admin exists, run:

```powershell
$env:BootstrapAdmin__Email="<initial-admin-email>"
$env:BootstrapAdmin__Password="<generated-password-at-least-12-characters>"
$env:BootstrapAdmin__FullName="<admin-display-name>"
dotnet run --project VendingAdSolution/VendingAdSystem -- --bootstrap-admin
Remove-Item Env:BootstrapAdmin__Email,Env:BootstrapAdmin__Password,Env:BootstrapAdmin__FullName
```

The command refuses to run when an admin account already exists.

## Run services

```powershell
dotnet run --project VendingAdSolution/VendingAdSystem
dotnet run --project VendingAdSolution/VendingAdWorker
```

Run the worker only when Redis and RabbitMQ are enabled and reachable.

Local endpoints:

- Web: `http://localhost:8080`.
- Liveness: `http://localhost:8080/health/live`.
- Readiness: `http://localhost:8080/health/ready`.
- Metrics: `http://localhost:8080/metrics`.

## Verify

```powershell
dotnet test VendingAdSolution/VendingAdSolution.sln --configuration Release
dotnet list VendingAdSolution/VendingAdSolution.sln package --vulnerable --include-transitive
```

Use `e2e/README.md`, `k6/README.md`, and `smoke-tests/critical-flows.sh` for environment-level verification.

## Device API and updates

Device API requests authenticate with `X-Device-Secret` or `Authorization: Bearer <secret>`.
Registration is rate-limited by client address, and subsequent requests are rate-limited by device code.

Only Admin users can upload a mobile APK.
The server validates the APK archive and publishes a SHA-256 checksum that the mobile client verifies before installation.

See the repository-level `HANDOVER.md`, `DEPLOYMENT.md`, `RUNBOOK.md`, and `SECURITY.md` before production release or ownership transfer.
