# VendAD — Vending Machine Ad System

## Quick Start (Visual Studio 2022)

### Step 1 — Open Solution
Double-click `VendingAdSolution.sln`

### Step 2 — Set Connection String
Open `VendingAdSystem/appsettings.json` and update:
```json
"DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=VendingAdDb;Trusted_Connection=True;TrustServerCertificate=True;"
```
- **(localdb)\mssqllocaldb** works out-of-the-box with VS2022 — no SQL Server install needed.
- For a full SQL Server instance: `Server=YOUR_SERVER;Database=VendingAdDb;Trusted_Connection=True;TrustServerCertificate=True;`

### Step 3 — Run (F5)
- VS2022 will restore NuGet packages automatically.
- The DB schema is **auto-created on first run** — no migrations needed.
- Browser opens at **http://localhost:5000**

---

## Solution Structure

```
VendingAdSolution/
├── VendingAdSolution.sln               ← Open this in VS2022
│
├── VendingAdSystem/                    ← ASP.NET Core 8 Web + API
│   ├── Controllers/
│   │   ├── PlaylistController.cs       ← GET /api/playlist/{deviceCode}
│   │   ├── HeartbeatController.cs      ← POST /api/heartbeat
│   │   ├── MediaController.cs          ← POST /api/media/upload
│   │   └── DashboardController.cs      ← MVC pages
│   ├── Models/Models.cs                ← Device, Media, Campaign
│   ├── Data/AppDbContext.cs            ← EF Core context
│   ├── Views/
│   │   ├── Dashboard/Index.cshtml      ← Device status dashboard
│   │   └── Dashboard/Upload.cshtml     ← Video upload page
│   ├── wwwroot/uploads/                ← Uploaded videos served from here
│   ├── Properties/launchSettings.json ← VS2022 run config (port 5000)
│   ├── appsettings.json               ← ← UPDATE CONNECTION STRING HERE
│   └── Program.cs
│
└── VendingAdFlutter/                   ← Flutter Android app
    ├── lib/main.dart                   ← ← SET kBaseUrl + kDeviceCode HERE
    ├── pubspec.yaml
    └── android/app/src/main/AndroidManifest.xml
```

---

## API Reference

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET`  | `/api/playlist/{deviceCode}` | Returns assigned video URL |
| `POST` | `/api/heartbeat` | Updates LastSeen; body: `{"deviceCode":"TABLET-001"}` |
| `POST` | `/api/media/upload` | Multipart form: `file` + `deviceCode` |

---

## Flutter App Setup

```bash
cd VendingAdFlutter

# 1. Edit lib/main.dart — set your server IP and device code:
#    const String kBaseUrl    = 'http://192.168.1.100:5000';
#    const String kDeviceCode = 'TABLET-001';

# 2. Install packages
flutter pub get

# 3. Run on connected tablet
flutter run

# 4. Build release APK
flutter build apk --release
# APK at: build/app/outputs/flutter-apk/app-release.apk
```

### Tablet behavior
| Event | Action |
|-------|--------|
| App launch | Send heartbeat, fetch & download video, start loop |
| Every 60 sec | Send heartbeat → keeps status Green on dashboard |
| Every 5 min | Re-check playlist → picks up new campaign assignments |
| Video cached | Re-download skipped if server URL unchanged |

---

## Dashboard

- **Green** = heartbeat received within last **5 minutes**
- **Red** = last heartbeat was > 5 minutes ago (or never)
- Dashboard auto-refreshes every **30 seconds**

---

## Production Notes
- [ ] Switch to HTTPS and remove `usesCleartextTraffic` in AndroidManifest
- [ ] Add authentication (ASP.NET Identity or Azure AD B2C)
- [ ] Move uploads to Azure Blob Storage
- [ ] Use proper EF Core migrations (`dotnet ef migrations add Initial`)
- [ ] Set a unique `kDeviceCode` per tablet (read from Android device ID or MDM)
