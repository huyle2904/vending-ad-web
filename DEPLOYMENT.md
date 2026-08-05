# Render Deployment Guide

Muc tieu: deploy duoc ngay tren Render va giu duoc file upload sau moi lan restart/redeploy.

## 1) Service Layout

`render.yaml` la source of truth cho production:

- 1 web service Docker: `vending-ad-system`
- 1 PostgreSQL database: `vending-ad-db`
- 1 persistent disk mount tai `/data`

Blueprint hien tai duoc set cho:

- Web plan: `starter`
- Postgres plan: `basic-256mb`
- Upload path: `/data/uploads`
- EF migrations chay trong pre-deploy command: `dotnet VendingAdSystem.dll --migrate`
- App khoi dong voi `Database__ApplyMigrationsOnStartup=false`

## 2) Render Deploy Steps

1. Push branch len GitHub/GitLab.
2. Vao Render, chon `New` -> `Blueprint`.
3. Ket noi repo va chon branch can deploy.
4. Render doc file `render.yaml` o root repo va tao web service + PostgreSQL.
5. Render chay pre-deploy migration va chi deploy neu migration thanh cong.
6. Sau khi deploy xong, kiem tra:
   - `GET /health/live`
   - `GET /health/ready`
   - `GET /metrics`

Lan deploy dau tien khong tao admin mac dinh.
Tao admin dau tien bang Render Shell va cac bien `BootstrapAdmin__Email`, `BootstrapAdmin__Password`, `BootstrapAdmin__FullName`, sau do chay `dotnet VendingAdSystem.dll --bootstrap-admin` va xoa cac bien bootstrap.

## 3) Why This Config

- Web service khong de `free` vi Render chi ho tro persistent disk cho paid services.
- Uploads duoc mount vao `/data/uploads` de tranh mat file sau redeploy.
- Docker image co cai `ffmpeg`/`ffprobe` de video upload validation van hoat dong tren Render.
- App chi dung PostgreSQL, connection string lay truc tiep tu Render Postgres resource.

## 4) Initial Data

Chi dung seed data tren moi truong demo co the xoa bo.
Script tu choi chay neu khong co flag xac nhan va khong nen dung voi production:

```powershell
.\scripts\seed-fake-data-postgres.ps1 `
  -Connection "host=<demo-host> port=5432 dbname=<db-name> user=<db-user> password=<db-password> sslmode=require" `
  -DemoUsername "<disposable-user>" `
  -DemoPassword "<generated-password>" `
  -DeviceSecretPrefix "<generated-prefix>" `
  -AllowDisposableSeed
```

Script se tao:

- Mot disposable demo user voi credential do nguoi chay script cung cap
- 50 devices: `DEVICE-001` -> `DEVICE-050`

## 5) Verification Checklist

- Login bang disposable demo user vua tao
- Upload video thanh cong
- File upload ton tai sau mot lan restart service
- Khong co migration error trong startup logs

## 6) Rollback

1. Redeploy commit truoc do.
2. Neu migration moi khong backward-compatible, restore database backup.
3. Kiem tra lai `/health/live` va `/health/ready`.
