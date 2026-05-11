# Project Context For Codex Local

## Project
- Repository: `/workspaces/vending-ad`
- Main solution: `VendingAdSolution/VendingAdSolution.sln`
- Main project: `VendingAdSolution/VendingAdSystem/VendingAdSystem.csproj`
- Framework: ASP.NET Core MVC/Web API on .NET 8
- Database: SQLite
- Runtime URL: `http://localhost:5000`

## Communication Preference
- Use Vietnamese when talking to user.
- Be concise and practical.
- Avoid over-engineering.
- Make small, safe refactors.

## Main Goal
Build clean company-style foundation for vending ad system inside single ASP.NET project.

Target style: modular monolith in one ASP.NET app.

## Current Product Direction
System now moving away from "playlist holds device + time" model.

New business flow:
- User upload video into personal video library.
- User may create own playlist from videos in library.
- User may create playback schedule from:
  - one or many single videos
  - or one playlist
- User then chooses devices for schedule.
- Schedule uses same time window for all selected devices.
- Different time windows require separate schedule.
- No support for cross-midnight playback.

## Time Rules
- User input is Vietnam time (GMT+7).
- UI display is Vietnam time.
- Business input/output is Vietnam time.
- DB stores UTC for `DateTime` fields.
- `StartTime` / `EndTime` are local Vietnam time in day, stored as `TimeSpan`.
- Do not use `DateTime.Now` for persistence or business checks.
- Do not support cross-day time range like `22:00 -> 02:00`.
- If user wants playback across days, they set:
  - one fixed time window
  - one date range from day A to day B

## Planned Data Model Direction
Keep:
- `User`
- `Admin`
- `Device`
- `Media`
- `Playlist`
- `PlaylistItem`

Add new schedule model:
- `PlaybackSchedule`
- `PlaybackScheduleDevice`
- `PlaybackScheduleItem`

## Meaning of Core Concepts
### Media
- Personal video library
- Upload creates only `Media`
- No device, no playback time

### Playlist
- Optional template created by user
- Contains ordered videos from library
- No device/time in new business logic

### PlaybackSchedule
- Real playback plan
- Chooses source:
  - single videos
  - or playlist
- Chooses devices
- Has date range + time range
- Playback API reads from active schedules

## Schedule Rules
- 1 device must not have 2 active schedules that overlap in same date/time range.
- Same device + same active time range => reject or require edit existing schedule.
- Playback order comes from `OrderIndex`.
- Playback API should keep old URL if possible:
  - `/api/portal/playlist/{deviceCode}`
- Backend logic may change, response should still be ordered video list.

## Admin Direction
Admin should manage:
- users
- devices
- videos
- playlists
- schedules

User management rules:
- no hard delete user
- admin can edit user info
- admin can toggle `IsActive`
- inactive user keeps data but cannot login

Admin video management:
- global list of all videos
- filter/search by user, playlist, device, keyword
- delete video if needed

Admin playlist management:
- global list of all playlists
- filter/search by user, device, keyword
- edit playlist
- delete playlist

## Completed Refactor
- Moved entities into `Domain/Entities`.
- Added DTO layer in `Application/DTOs`.
- Added service layer in `Application/Services`.
- Moved DbContext into `Infrastructure/Persistence/AppDbContext.cs`.
- Added generic repository.
- Added DI registration.
- Added SQLite seeder.
- Cleaned Program startup.
- Added portal/admin UI and API routes.
- Build was clean last checked.

## Important Files
- `VendingAdSolution/VendingAdSystem/Program.cs`
- `VendingAdSolution/VendingAdSystem/Domain/Entities/*.cs`
- `VendingAdSolution/VendingAdSystem/Application/DTOs/*.cs`
- `VendingAdSolution/VendingAdSystem/Application/Services/*.cs`
- `VendingAdSolution/VendingAdSystem/Infrastructure/Persistence/AppDbContext.cs`
- `VendingAdSolution/VendingAdSystem/Infrastructure/DependencyInjection.cs`
- `VendingAdSolution/VendingAdSystem/Infrastructure/Seed/DatabaseSeeder.cs`
- `VendingAdSolution/VendingAdSystem/Controllers/AdminController.cs`
- `VendingAdSolution/VendingAdSystem/Controllers/PortalController.cs`
- `VendingAdSolution/VendingAdSystem/Controllers/PortalApiController.cs`
- `VendingAdSolution/VendingAdSystem/Views/Admin/*.cshtml`
- `VendingAdSolution/VendingAdSystem/Views/Portal/*.cshtml`
- `VendingAdSolution/VendingAdSystem/Views/PortalDevices/*.cshtml`
- `VendingAdSolution/VendingAdSystem/Views/Shared/_LayoutSidebar.cshtml`

## Known Notes
- `VendADS/` root figma copy was deleted and should stay ignored.
- `dotnet run` may fail if port `5000` already in use.
- Use another port if needed:

```bash
ASPNETCORE_URLS=http://localhost:5001 dotnet run --project VendingAdSolution/VendingAdSystem
```

## Auth Details
- Admin seed:
  - username/email: `admin@admin`
  - password: `admin@admin`
- Demo user may exist:
  - username/email: `test@test`
  - password: `test@test`
- Admin default password for created users:
  - `TD@12345`

## Current Focus / Next Work
1. Refactor system from playlist-device-time model to playback-schedule model.
2. Keep upload as personal video library only.
3. Keep playlist as optional template only.
4. Add playback schedule creation UI.
5. Ensure timezone handling remains Vietnam-first for input/output, UTC for stored DateTime.
6. Keep build clean after each phase:

```bash
dotnet build VendingAdSolution/VendingAdSolution.sln
```

## Implementation Plan
### Phase 1 - Core Model
- Add `PlaybackSchedule`.
- Add `PlaybackScheduleDevice`.
- Add `PlaybackScheduleItem`.
- Update `AppDbContext`.
- Update SQLite seeder.
- Register new services.

### Phase 2 - Video Library
- Upload creates only `Media`.
- Remove device/time from upload flow.
- Keep video library page for browsing and deleting.

### Phase 3 - Playlist Template
- Playlist becomes reusable template only.
- Manage video order in playlist.
- Remove device/time from playlist business flow.

### Phase 4 - Playback Schedule
- Add `/portal/playback` or `/portal/schedules`.
- Create schedule from:
  - selected videos, or
  - selected playlist
- Choose devices.
- Choose date range + time range.
- Validate overlap per device.
- Add edit/delete/toggle.

### Phase 5 - Playback API
- Keep endpoint URL if possible.
- Change backend to read from active schedules.
- Return ordered media list for device.

### Phase 6 - Admin
- Add admin schedules management.
- Keep admin user/video/playlist pages.
- Add filters/search where useful.

## Important Constraints
- No per-device time in same schedule.
- Same schedule time applies to all selected devices.
- Different time windows require separate schedules.
- Do not support cross-midnight time range.
- Keep changes small and reversible.
- Preserve current behavior unless user explicitly asks to change it.
