# k6 Load Testing

Run these scripts only against a disposable or explicitly approved environment.
Never commit device credentials or place production secrets in command history shared with others.

## Single device

Set an existing device code and secret before running smoke, load, or stress tests:

```powershell
$env:BASE_URL = 'http://localhost:8080'
$env:DEVICE_CODE = '<test-device-code>'
$env:DEVICE_SECRET = '<test-device-secret>'
k6 run k6/smoke.js
k6 run k6/load.js
k6 run k6/stress.js
```

The scripts fail immediately when either credential is missing.

## Device fleet

The fleet script expects disposable devices whose codes and secrets share known prefixes:

```powershell
$env:BASE_URL = 'http://localhost:8080'
$env:DEVICE_COUNT = '20'
$env:DEVICE_PREFIX = '<test-device-code-prefix>'
$env:DEVICE_SECRET_PREFIX = '<test-device-secret-prefix>'
k6 run k6/mobile-fleet.js
```

Use `scripts/seed-fake-data-postgres.ps1` only on a disposable database and provide all credentials explicitly.
Delete disposable devices and rotate any retained device secrets after testing.
