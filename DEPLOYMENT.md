# Production Deployment Guide (Task 1.14)

Muc tieu: quy trinh deploy co the lap lai, de kiem soat va rollback an toan.

## 1) Pre-deployment Checklist
- Confirm branch da merge vao `main`
- CI xanh (build + test)
- DB migration script da review
- Backup moi nhat co san
- Env vars production da cap nhat

## 2) Environment Variables
Bat buoc:
- `ASPNETCORE_ENVIRONMENT=Production`
- `ConnectionStrings__DefaultConnection=<sql-server-connection-string>`
- `Database__ApplyMigrationsOnStartup=false`
- `Database__EnsureCreatedOnStartup=false`
- `Database__ResetOnStartup=false`
- `Database__ResetSchemaOnStartup=false`
- `Seed__EnableDemoData=false`
- `Seed__AllowDemoDataOutsideDevelopment=false`

Khuyen nghi:
- `AllowedHosts=<production-domain>`
- `VideoValidation__FfprobeEnabled=true`
- `VideoValidation__RequireFfprobe=true`

## 3) Database Migration Procedure
Thuc hien migration trong buoc deploy co kiem soat, khong chay auto tren startup:
```bash
dotnet ef database update --project VendingAdSolution/VendingAd.Infrastructure --startup-project VendingAdSolution/VendingAdSystem
```

## 4) Deploy Steps
1. Pull image/build artifact moi nhat.
2. Stop old instance theo rolling strategy.
3. Apply migration.
4. Start new instance.
5. Verify health checks va smoke tests.

## 5) Health Verification
- `GET /health/live` => 200
- `GET /health/ready` => 200
- `GET /metrics` => 200

## 6) Post-deployment Verification
- Dang nhap admin thanh cong
- Device API auth hoat dong
- Upload video + tao schedule thanh cong
- Khong co log error nghiem trong trong 15 phut dau

## 7) Rollback Procedure
1. Route traffic ve version truoc.
2. Neu migration khong backward-compatible, restore DB backup.
3. Re-run health checks.
4. Ghi nhan incident + root cause.
