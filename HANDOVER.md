# VendingCMS Handover

## Repository and release source

- Repository: `https://github.com/huyle2904/vending-ad-web`.
- Production releases must come from the protected `main` branch after CI passes.
- The ASP.NET Core web application is `VendingAdSolution/VendingAdSystem`.
- The optional event worker is `VendingAdSolution/VendingAdWorker`.
- The mobile client is maintained in the separate `vending-ad-mobile` repository.

## Required toolchain

- .NET SDK 8.x.
- Docker with Docker Compose for local PostgreSQL, Redis, RabbitMQ, Seq, Prometheus, and Grafana.
- FFmpeg and ffprobe for production-equivalent video validation.
- Node.js 22 for Playwright checks.

## Clean-machine setup

1. Copy `.env.example` to `.env` and replace every placeholder with a local-only value.
2. Copy both `appsettings.Development.example.json` files to `appsettings.Development.json` beside the originals and replace every placeholder.
3. Start local infrastructure with `docker compose -f docker-compose.infra.yml up -d postgres redis rabbitmq`.
4. Restore and migrate with `dotnet restore VendingAdSolution/VendingAdSolution.sln` and `dotnet run --project VendingAdSolution/VendingAdSystem -- --migrate`.
5. Bootstrap the first admin using the environment variables and command documented in `VendingAdSolution/README.md`.
6. Start the web app with `dotnet run --project VendingAdSolution/VendingAdSystem`.
7. Start the worker in a second terminal only when Redis and RabbitMQ are enabled.

Never commit `.env`, `appsettings.Development.json`, keystores, database exports, uploaded media, or credentials.

## Verification before release

```powershell
dotnet build VendingAdSolution/VendingAdSolution.sln --configuration Release
dotnet test VendingAdSolution/VendingAdSolution.sln --configuration Release --no-build
dotnet list VendingAdSolution/VendingAdSolution.sln package --vulnerable --include-transitive
npm ci --ignore-scripts
npm audit --audit-level=high
```

Run the Playwright, smoke, and k6 suites against a disposable environment when the corresponding scope changes.

## Production configuration

The following values must be managed by the deployment platform, not Git:

- `ConnectionStrings__DefaultConnection`.
- `AllowedHosts`.
- `UploadsPath`.
- Redis and RabbitMQ connection settings when those services are enabled.
- Data Protection key storage when the service scales beyond one instance.
- Monitoring, alerting, backup, and log-retention credentials.

Render runs `dotnet VendingAdSystem.dll --migrate` as a pre-deploy command.
Normal production startup keeps `Database__ApplyMigrationsOnStartup=false`.

## Ownership transfer checklist

- [ ] Transfer the GitHub repository to the company organization or grant company owners administrative access.
- [ ] Transfer the Render workspace, service, PostgreSQL database, disk, billing, and deploy permissions.
- [ ] Transfer DNS and custom-domain ownership.
- [ ] Transfer monitoring, alerting, backup, and incident-response access.
- [ ] Record production URLs and the responsible on-call team in the internal company runbook.
- [ ] Verify a database backup and restore on a disposable database.
- [ ] Confirm the insecure historical `admin` account is absent and a company-owned admin can sign in.
- [ ] Remove former employees and personal accounts after the receiving team confirms access.
- [ ] Revoke the APIPRO credential that appeared in commit `a6e71842` and coordinate a history rewrite before declaring the repository secret-clean.

## Mobile update trust chain

Only an authenticated Admin can publish an APK.
The server validates the APK archive and publishes its SHA-256 checksum.
The mobile app refuses an update when the checksum is absent or does not match.
The Android package must also be signed with the company release key documented in the mobile repository handover.
