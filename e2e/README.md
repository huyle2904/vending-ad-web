# Playwright E2E

Run the smoke suite against the local app on port 8080:

```powershell
npm run e2e
```

The suite runs sequentially because the tests create portal data with the same user account.

Run with a visible browser:

```powershell
npm run e2e:headed
```

Override the app URL when needed:

```powershell
$env:E2E_BASE_URL = 'http://localhost:8080'
npm run e2e
```

The video upload test is opt-in because it needs a real portal user and a real video file:

```powershell
$env:E2E_USER = 'your-user'
$env:E2E_PASSWORD = 'your-password'
$env:E2E_SAMPLE_VIDEO = 'C:\path\to\sample.mp4'
npm run e2e
```

The Playwright config uses the installed Microsoft Edge browser, so it does not need to download Chromium.

Run the full portal flow with the local default test account and sample video:

```powershell
npm run e2e -- e2e/portal-full-flow.spec.ts
```

This covers login, portal navigation, video upload with thumbnail, playlist creation, adding video to a playlist, YouTube form validation/add, and schedule creation when the account has a claimed device.

Defaults used by `portal-full-flow.spec.ts`:

- `E2E_USER`: `cococaca`
- `E2E_PASSWORD`: `TD@12345`
- `E2E_SAMPLE_VIDEO`: `C:\Users\TD-997\Downloads\VendingAD1.mp4`
