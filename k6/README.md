# k6 Load Testing

Load testing scripts cho VendingAd CMS API.

## Prerequisites

1. **Cài k6:**
   ```bash
   # macOS
   brew install k6
   
   # Linux (Ubuntu/Debian)
   sudo gpg --no-default-keyring --keyring /usr/share/keyrings/k6-archive-keyring.gpg \
     --keyserver hkp://keyserver.ubuntu.com:80 \
     --recv-keys C5AD17C747E3415A3642D57D77C6C491D6AC1D69
   echo "deb [signed-by=/usr/share/keyrings/k6-archive-keyring.gpg] https://dl.k6.io/deb stable main" | \
     sudo tee /etc/apt/sources.list.d/k6.list
   sudo apt-get update && sudo apt-get install -y k6
   
   # Windows
   choco install k6
   ```

2. **Start infrastructure và app:**
   ```bash
   # Terminal 1: Start SQL Server
   docker compose -f docker-compose.infra.yml up -d sqlserver
   
   # Terminal 2: Start app với demo data
   cd VendingAdSolution
   Database__ApplyMigrationsOnStartup=true \
   Seed__EnableDemoData=true \
   dotnet run --no-launch-profile --project VendingAdSystem
   ```

3. **Verify app ready:**
   ```bash
   curl http://localhost:8080/health/ready
   ```

## Demo Device Credentials

Khi `Seed:EnableDemoData=true`, app tạo 20 devices với credentials:

| Device Code | Secret | Location |
|---|---|---|
| TAB-01 | `dev-secret-TAB-01` | Vincom Center |
| TAB-02 | `dev-secret-TAB-02` | Ben Thanh Market |
| TAB-03 | `dev-secret-TAB-03` | Bitexco Tower |
| ... | `dev-secret-TAB-{XX}` | ... |
| TAB-20 | `dev-secret-TAB-20` | Indochina Plaza |

**Format:** `dev-secret-{DeviceCode}`

## Running Tests

### 1. Smoke Test (1 VU, 1 phút)
Sanity check sau mỗi deploy:
```bash
k6 run k6/smoke.js
```

Với custom device:
```bash
DEVICE_CODE=TAB-05 DEVICE_SECRET=dev-secret-TAB-05 k6 run k6/smoke.js
```

### 2. Load Test (50 VUs, ~10 phút)
Kiểm tra normal load:
```bash
k6 run k6/load.js
```

### 3. Stress Test (200 VUs, ~17 phút)
Tìm breaking point:
```bash
k6 run k6/stress.js
```

### Test remote server:
```bash
BASE_URL=https://your-app.onrender.com \
DEVICE_CODE=TAB-01 \
DEVICE_SECRET=dev-secret-TAB-01 \
k6 run k6/load.js
```

## Thresholds

| Metric | Threshold | Mô tả |
|---|---|---|
| `http_req_failed` | < 5% | Error rate phải dưới 5% |
| `http_req_duration` | p95 < 2s | 95% requests phải < 2s |
| `http_req_duration{endpoint:playback}` | p95 < 1s | Playback API < 1s |
| `http_req_duration{endpoint:heartbeat}` | p95 < 500ms | Heartbeat < 500ms |

## Expected Results

**Smoke test (pass):**
```
✓ http_req_duration............: p(95) < 2000ms
✓ http_req_failed..............: rate < 1%
✓ checks.......................: 100%
```

**Load test (50 VUs):**
```
http_req_duration..............: avg=50ms p(95)=200ms
http_reqs......................: ~30 req/s
iteration_duration.............: avg=1.5s
```

**Stress test (200 VUs):**
- Mục tiêu: tìm breaking point (khi nào error rate > 10% hoặc p95 > 5s)
- Quan sát: memory usage, CPU, database connections

## Troubleshooting

**401 Unauthorized:**
- Kiểm tra `Seed:EnableDemoData=true` khi start app
- Verify device tồn tại: `curl http://localhost:8080/api/mobile/devices/TAB-01 -H "X-Device-Secret: dev-secret-TAB-01"`

**Connection refused:**
- App chưa start hoặc chưa ready
- Check: `curl http://localhost:8080/health/ready`

**High error rate:**
- Database connection pool exhausted
- Redis/RabbitMQ down (nếu enabled)
- Check app logs: `docker logs vendingad-app` hoặc console output
