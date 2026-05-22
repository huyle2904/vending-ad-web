# Operational Runbook (Task 1.14)

## Service Overview
- Web app: `VendingAdSystem`
- Health endpoints: `/health/live`, `/health/ready`
- Metrics endpoint: `/metrics`

## Standard Operating Procedures

### 1) Restart Service
1. Check current health endpoint.
2. Restart service/container.
3. Re-check health endpoints.

### 2) Incident: DB Connectivity Loss
1. Kiem tra connection string va network.
2. Kiem tra SQL Server status.
3. Kiem tra app logs va retry behavior.
4. Neu can, fail over hoac rollback ban deploy gan nhat.

### 3) Incident: Upload Failure
1. Kiem tra writable path `UploadsPath`.
2. Kiem tra dung luong disk.
3. Kiem tra ffprobe availability.
4. Retry voi file mau hop le.

### 4) Incident: Elevated 5xx Rate
1. Kiem tra log exception theo correlation id.
2. Kiem tra health ready va database status.
3. Neu do release moi, rollback ngay.

## Monitoring URLs
- Health live: `/health/live`
- Health ready: `/health/ready`
- Metrics: `/metrics`

## Escalation
- Level 1: On-call backend engineer
- Level 2: Tech lead
- Level 3: Infra owner

## Recovery Validation
- Login admin/user ok
- Device APIs ok
- Upload va schedule flow ok
- Error rate ve normal
