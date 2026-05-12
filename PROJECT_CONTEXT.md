# Project Context

## Project
- Repository: `/workspaces/vending-ad`
- GitHub repo: `https://github.com/huyle2904/vending-ad-web`
- Current branch for coding: `dev`
- Default PR target branch: `main`
- Main solution: `VendingAdSolution/VendingAdSolution.sln`
- Main project: `VendingAdSolution/VendingAdSystem/VendingAdSystem.csproj`
- Framework: ASP.NET Core MVC/Web API on .NET 8
- Database: EF Core multi-provider. Local default is SQLite; Render uses PostgreSQL; future local MSSQL is supported by config.
- App is now web-only. Old mobile folder `VendingAdFlutter` was removed from tracked repo.

## Communication
- Talk to user in Vietnamese.
- Be concise, practical, direct.
- Prefer small safe changes.

## Current Product Direction
- `Media` = personal video library.
- `Playlist` = reusable template only.
- `PlaybackSchedule` = real playback plan.
- Device playback reads from active schedules.
- `Device Wall` is only a web simulator for quick testing, not the real production player.
- Real production playback target is a future mobile/TV box app installed on vending machines.

## UI Direction
- Keep CMS UI simple, direct, and consistent.
- Use one shared visual language across portal and admin.
- Prefer white/light surfaces, one primary blue, minimal decoration.
- Avoid colorful gradients, emoji icons, and marketing-style visuals.
- App UI text is now standardized to Vietnamese.
- Keep internal code identifiers in English, but all visible user-facing text should stay Vietnamese.

## Business Flow
- User uploads video into personal library.
- User creates playlist template from library videos.
- User creates schedule from either:
  - selected single videos
  - or one playlist template
- User chooses one or more owned devices.
- Same schedule time window applies to all selected devices.
- Different time windows require separate schedules.
- No cross-midnight playback.

## Time Rules
- User input is Vietnam time.
- UI output is Vietnam time.
- `DateTime` persisted in UTC.
- `StartTime` / `EndTime` stored as `TimeSpan` in Vietnam local day time.
- Do not use `DateTime.Now` for persistence or business checks.

## Core Models
- Keep:
  - `User`
  - `Admin`
  - `Device`
  - `Media`
  - `Playlist`
  - `PlaylistItem`
- Active schedule model:
  - `PlaybackSchedule`
  - `PlaybackScheduleDevice`
  - `PlaybackScheduleItem`

## Important Meaning
### Media
- Upload creates only `Media`.
- No device/time attached.

### Playlist
- Optional reusable template.
- Ordered list of user videos.
- No device/time attached.

### PlaybackSchedule
- Real playback plan.
- Has devices, date range, time range, ordered media snapshot.
- Playback API reads from this model.

### Mobile / TV Box App Direction
- No mobile code exists in this repo yet.
- Mobile app will run fullscreen on TV box/vending machine.
- App must play according to schedules created by end user on web.
- App must download videos to local storage before playback; do not stream directly.
- App should keep playing cached content if network is temporarily unavailable where possible.
- App should use server-provided UTC time/anchor instead of guessing local time.

## Current Architecture
- `Controllers/*`
  - MVC pages + API endpoints
- `Application/Services/*`
  - business logic
- `Application/DTOs/*`
  - request/response objects
- `Infrastructure/Persistence/AppDbContext.cs`
  - EF Core context
- `Infrastructure/Seed/DatabaseSeeder.cs`
  - SQLite schema repair + provider-safe seed data
- `Infrastructure/Repositories/*`
  - generic repository

## Key Files
- `VendingAdSolution/VendingAdSystem/Controllers/PortalController.cs`
- `VendingAdSolution/VendingAdSystem/Controllers/PortalApiController.cs`
- `VendingAdSolution/VendingAdSystem/Controllers/AdminController.cs`
- `VendingAdSolution/VendingAdSystem/Application/Services/PlaybackScheduleService.cs`
- `VendingAdSolution/VendingAdSystem/Application/Services/PlaylistManagementService.cs`
- `VendingAdSolution/VendingAdSystem/Application/Services/PlaylistService.cs`
- `VendingAdSolution/VendingAdSystem/Infrastructure/Seed/DatabaseSeeder.cs`
- `VendingAdSolution/VendingAdSystem/Views/Portal/Videos.cshtml`
- `VendingAdSolution/VendingAdSystem/Views/Portal/Playlist.cshtml`
- `VendingAdSolution/VendingAdSystem/Views/Portal/Schedules.cshtml`
- `VendingAdSolution/VendingAdSystem/Views/PortalDevices/Index.cshtml`
- `VendingAdSolution/VendingAdSystem/wwwroot/css/site.css`

## Auth Details
- Admin seed:
  - `admin@admin` / `admin@admin`
- Demo user:
  - `test@test` / `test@test`
- Admin-created user default password:
  - `TD@12345`

## What Is Done
- Removed legacy `Campaign` and `PlaylistDevice` flow from active logic.
- Converted playlist to template-only model.
- Added playback schedule domain + services + UI.
- Kept playback API URL pattern `/api/portal/playlist/{deviceCode}`.
- Added admin schedule list/filter/toggle/delete page.
- Added DB seeder repair for old `Playlists` schema and `PlaybackSchedules` columns.
- Split repository to web-only.
- Added GitHub Actions CI workflow for PR to `main` and pushes to `dev`/`main`.
- Added Device Wall web simulator for multiple devices.
- Added live schedule item editing and drag-drop reorder in schedule detail modal.
- Upgraded portal dashboard with correct current/upcoming schedule logic.
- Upgraded login UI and standardized global CMS styling.
- Installed `ui-ux-pro-max` skill for OpenCode in `.opencode/skills/ui-ux-pro-max/`.
- Added quick-play flow on portal `Devices` and `Dashboard` cards using existing immediate schedule flow.
- Added schedule status tag `Đã lên lịch` and distinct color for scheduled items.
- Synchronized major portal/admin UI text to Vietnamese.
- Installed `find-skills`, `frontend-design`, and `web-design-guidelines` skills for OpenCode.
- Added Render Docker deploy config with Render PostgreSQL support.
- Added DB provider switch via `DatabaseProvider` (`Sqlite`, `Postgres`, `SqlServer`).
- Added device claim flow: app/device registers first, receives 6-digit `ClaimCode`; user/admin can claim/assign device later.
- Admin device page should not create devices manually; devices come from app registration. Admin can assign only unassigned devices to users and must not edit device location.
- Added admin statistics dashboard plan/implementation work in progress: `/admin` should be a chart/statistics page, sidebar item label `Thống kê`.

## Mobile API Plan (Next Session Priority)
Implement backend APIs for future mobile/TV box app. Keep existing portal APIs for compatibility.

### API Namespace
- Use new namespace: `/api/mobile/...`
- Do not remove existing `/api/portal/playlist/{deviceCode}` yet.

### Needed Endpoints
1. `GET /api/mobile/devices/{deviceCode}`
   - Returns device info and claim status.
   - If unclaimed, return `claimRequired = true` and current `claimCode`.
   - If claimed, return assigned user info.

2. `POST /api/mobile/heartbeat`
   - Phase 1 request can be `{ "deviceCode": "..." }`.
   - Response should include `serverTimeUtc`.
   - Later can include app version, current schedule/media, storage free, error state.

3. `GET /api/mobile/playback-state/{deviceCode}`
   - Main API for mobile playback.
   - If device does not exist: 404.
   - If device exists but is unclaimed: return 200 with `claimRequired = true`, `hasActiveSchedule = false`, `claimCode`.
   - If no active schedule: return 200 with `hasActiveSchedule = false`.
   - If active schedule: return schedule metadata + ordered video items.

### Playback State Response Shape
```json
{
  "success": true,
  "deviceCode": "TVBOX-001",
  "serverTimeUtc": "2026-05-12T08:30:00Z",
  "hasActiveSchedule": true,
  "claimRequired": false,
  "claimCode": null,
  "schedule": {
    "id": 12,
    "name": "Lịch sáng",
    "version": "12-638826354000000000-5-8",
    "isImmediate": false,
    "startDateUtc": "2026-05-12T00:00:00Z",
    "endDateUtc": "2026-05-20T23:59:59Z",
    "startTime": "08:00:00",
    "endTime": "11:30:00",
    "playbackAnchorUtc": "2026-05-12T01:00:00Z"
  },
  "items": [
    {
      "mediaId": 5,
      "fileName": "promo-1.mp4",
      "fileUrl": "https://domain/uploads/promo-1.mp4",
      "orderIndex": 0,
      "fileSize": 12345678,
      "checksum": null,
      "durationSeconds": null
    }
  ]
}
```

### Active Schedule Rules
- Device must exist, be active, and be assigned (`UserId != null`) for normal playback.
- Find schedule where:
  - `IsActive == true`
  - schedule contains the device
  - `StartDate <= serverTimeUtc <= EndDate`
  - current Vietnam local time is between `StartTime` and `EndTime`
- Priority:
  - immediate schedule first
  - then newest `CreatedAt`
  - then newest `StartDate`

### Schedule Version Rule
- Phase 1 has no `UpdatedAt`, so version should be derived from schedule and item list.
- Suggested version:
  - `schedule.Id`
  - `schedule.CreatedAt.Ticks`
  - ordered media IDs / item IDs / order indexes
- App will use this version later to know whether it must re-sync/download.

### Playback Anchor Rule
- Normal schedule:
  - Anchor = current Vietnam date + schedule `StartTime`, converted to UTC.
- Immediate schedule:
  - Anchor = `ImmediateStartedAt` if present.
  - Fallback to normal schedule anchor.

### Duration / Download Rule
- Phase 1 API returns `durationSeconds = null`.
- Mobile app must download video from `fileUrl` to local storage before playback.
- Mobile app reads duration metadata from downloaded local file and caches it locally.
- Do not stream directly from `fileUrl`; `fileUrl` is only the download source.
- Later phase can add `Media.DurationSeconds` and checksum.

### Suggested Files To Add/Modify
- Add `Application/DTOs/MobilePlaybackDtos.cs`
- Add `Application/Services/MobilePlaybackService.cs`
- Add `Controllers/MobileApiController.cs`
- Register service in `Infrastructure/DependencyInjection.cs`
- Keep `PlaylistService` and existing portal playlist endpoint stable.

### Mobile Behavior To Remember
- Future mobile app should:
  - register device and display claim code until claimed
  - heartbeat periodically
  - poll playback-state periodically
  - download missing videos
  - play fullscreen from local file only
  - if schedule version changes, re-sync immediately in Phase 1
  - if network is lost, continue cached playlist when possible

## Database Provider / Deploy Notes
- Current Render target uses PostgreSQL from `render.yaml`:
  - `DatabaseProvider=Postgres`
  - `ConnectionStrings__DefaultConnection` comes from Render database `vending-ad-db`
- Local default remains SQLite in `appsettings.json`:
  - `DatabaseProvider=Sqlite`
  - `DefaultConnection=Data Source=vendingad.db`
- To switch later to local MSSQL, change config/env only:
  - `DatabaseProvider=SqlServer`
  - `ConnectionStrings__DefaultConnection=Server=localhost;Database=VendingAdDb;Trusted_Connection=True;TrustServerCertificate=True;`
- No data migration from Render PostgreSQL to MSSQL is required unless user explicitly asks; user said data does not need to be kept.
- `Infrastructure/DependencyInjection.cs` chooses EF provider by `DatabaseProvider`.
- `DatabaseSeeder.Seed` runs SQLite repair only for SQLite; PostgreSQL/MSSQL use `EnsureCreated()` plus EF seed data.
- Avoid adding provider-specific raw SQL unless guarded by provider checks (`db.Database.IsSqlite()`, etc.).
- Uploads on Render still use `/data/uploads`; without persistent disk, uploaded video files may be temporary even though DB is online.

## Current In-Progress Work
- CMS-wide Vietnamese localization and UI polish completed for current scope.
- Next likely work:
  - final visual QA on mobile and desktop
  - any small text cleanup spotted during manual review
  - commit stable batch to `dev`

## Next Likely Work
1. Do final manual QA on dashboard, devices, schedules, playlist, videos, admin, profile, settings.
2. Clean any remaining English visible text if found.
3. Commit stable batch to `dev` when ready.
4. Create PR from `dev` to `main` when ready.

## Known Notes
- Repo is web-only now. Do not reintroduce old `VendADS` or Flutter code unless user explicitly asks.
- `opencode.json` is intentionally tracked because user wants it available on personal machine.
- `wwwroot/uploads/` is runtime data and ignored.
- Use another port if `5000`/`5001` already in use.
- `ui-ux-pro-max` skill installed under `.opencode/skills/ui-ux-pro-max/`.
- `frontend-design` and `web-design-guidelines` are installed under `.agents/skills/`.
- `find-skills` is installed under `.agents/skills/`.

## Commands
```bash
dotnet build VendingAdSolution/VendingAdSolution.sln
ASPNETCORE_URLS=http://localhost:5001 dotnet run --no-launch-profile --project VendingAdSolution/VendingAdSystem
```

## Important Constraints
- No per-device time inside same schedule.
- Same schedule time applies to all selected devices.
- No cross-midnight time range.
- Keep changes small and reversible.
- Prefer continuing existing service/repository style.
