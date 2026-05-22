# Troubleshooting Guide (Task 1.14)

## 1) Bad Request - Invalid Hostname
Nguyen nhan: `AllowedHosts` khong khop host hien tai.

Khac phuc:
- Development/Codespaces: dat `AllowedHosts=*` trong `appsettings.Development.json`
- Production: whitelist domain chinh xac

## 2) Login Fails Sau Deploy
- Kiem tra cookie secure policy va HTTPS termination
- Kiem tra forwarded headers (`X-Forwarded-Proto`)
- Kiem tra clock skew giua app node

## 3) Health Ready Fails
- Kiem tra DB connection string
- Kiem tra migration state
- Kiem tra Redis/RabbitMQ config (neu enabled)

## 4) Upload Video Fails
- Kiem tra `UploadsPath` co quyen ghi
- Kiem tra ffprobe path/availability
- Kiem tra kich thuoc file va content-type

## 5) Spike 429 Rate Limit
- Kiem tra traffic pattern cua device
- Dieu chinh `MobileRateLimiting` trong config
- Xac nhan khong co retry storm tu client

## 6) High 5xx Error Rate
- Tim correlation id trong logs
- Kiem tra release moi nhat va rollback neu can
- Verify DB va disk state
