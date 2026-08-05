import { expect, test, type Page } from '@playwright/test';

function requiredEnvironmentVariable(name: string): string {
  const value = process.env[name]?.trim();
  if (!value) {
    throw new Error(`${name} is required for the full portal E2E flow.`);
  }
  return value;
}

const credentials = {
  username: requiredEnvironmentVariable('E2E_USER'),
  password: requiredEnvironmentVariable('E2E_PASSWORD'),
};

const rawSampleVideo = requiredEnvironmentVariable('E2E_SAMPLE_VIDEO');
const sampleVideo = rawSampleVideo.replace(/[\u202A-\u202E]/g, '');
const runId = Date.now();

function uniqueFutureDate() {
  const date = new Date(Date.UTC(2035, 0, 1));
  date.setUTCDate(date.getUTCDate() + (runId % 300));
  return date.toISOString().slice(0, 10);
}

async function login(page: Page) {
  await page.goto('/account/login');
  await page.locator('#loginUsername').fill(credentials.username);
  await page.locator('#loginPassword').fill(credentials.password);
  await page.locator('button[type="submit"]').click();
  await page.waitForURL(/\/portal\/dashboard|\/admin|\/portal\/videos/, { timeout: 15_000 });
  expect(page.url()).toContain('/portal');
}

async function gotoPortal(page: Page, path: string) {
  await page.goto(path);
  await page.waitForLoadState('networkidle');
  await expect(page.locator('body')).toBeVisible();
}

test.describe.serial('portal full feature flow', () => {
  test('login and navigate core portal pages', async ({ page }) => {
    await login(page);

    await gotoPortal(page, '/portal/dashboard');
    await expect(page.locator('.dashboard-page')).toBeVisible();

    await gotoPortal(page, '/portal/videos');
    await expect(page.locator('#videoFile')).toBeAttached();

    await gotoPortal(page, '/portal/playlist');
    await expect(page.locator('#playlistNameInput')).toBeVisible();

    await gotoPortal(page, '/portal/schedules');
    await expect(page.locator('#scheduleCreateForm')).toBeAttached();

    await gotoPortal(page, '/portal/youtube');
    await expect(page.locator('#youtubeUrl')).toBeVisible();

    await gotoPortal(page, '/portal/devices');
    await expect(page.locator('body')).toContainText(/thiết bị|Device/i);
  });

  test('upload video, store thumbnail, create playlist, and add video', async ({ page }) => {
    await login(page);
    await gotoPortal(page, '/portal/videos');

    const uploadResponsePromise = page.waitForResponse((response) =>
      response.url().includes('/api/portal/upload') && response.request().method() === 'POST'
    );

    await page.locator('#videoFile').setInputFiles(sampleVideo);
    const uploadResponse = await uploadResponsePromise;
    expect(uploadResponse.ok()).toBeTruthy();

    const payload = await uploadResponse.json();
    expect(payload.fileUrl).toMatch(/^\/uploads\//);
    expect(payload.thumbnailUrl).toMatch(/^\/uploads\/.+\.thumb\.jpg$/);

    await expect(page.locator(`img[src="${payload.thumbnailUrl}"]`).first()).toBeVisible({ timeout: 15_000 });

    await gotoPortal(page, '/portal/playlist');
    const playlistName = `E2E playlist ${runId}`;
    await page.locator('#playlistNameInput').fill(playlistName);
    await Promise.all([
      page.waitForURL(/\/portal\/playlist/),
      page.locator('form[action="/portal/playlist/create"] button[type="submit"]').click(),
    ]);
    await expect(page.getByText(playlistName)).toBeVisible();

    const playlistCard = page.locator('.playlist-item-card', { hasText: playlistName }).first();
    await playlistCard.locator('button', { hasText: /Thêm video/i }).click();
    await expect(page.locator('#videoModal')).toBeVisible();
    await page.locator('#videoModal input[name="mediaIds"]').first().check();
    await Promise.all([
      page.waitForURL(/\/portal\/playlist/),
      page.locator('#videoModal button[type="submit"]').click(),
    ]);

    await expect(page.locator('.playlist-item-card', { hasText: playlistName }).first()).toContainText(/1 video|video/i);
  });

  test('youtube form validates and accepts a valid link', async ({ page }) => {
    await login(page);
    await gotoPortal(page, '/portal/youtube');

    const input = page.locator('#youtubeUrl');
    await expect(input).toBeVisible();

    await input.fill('not-a-url');
    await page.locator('form[action="/portal/youtube/add"] button[type="submit"]').click();
    await expect.poll(async () => input.evaluate((element: HTMLInputElement) => element.validationMessage)).not.toBe('');

    const youtubeUrl = 'https://www.youtube.com/watch?v=dQw4w9WgXcQ';
    await input.fill(youtubeUrl);
    await Promise.all([
      page.waitForURL(/\/portal\/youtube/),
      page.locator('form[action="/portal/youtube/add"] button[type="submit"]').click(),
    ]);

    await expect(page.locator('.video-list-row', { hasText: youtubeUrl }).first()).toBeVisible({ timeout: 10_000 });
  });

  test('create schedule when user has at least one device and media item', async ({ page }) => {
    await login(page);
    await gotoPortal(page, '/portal/schedules');

    const deviceCheckbox = page.locator('#scheduleCreateForm input[name="DeviceIds"]').first();
    const mediaCheckbox = page.locator('#scheduleCreateForm input[name="MediaIds"]').first();
    test.skip(await deviceCheckbox.count() === 0, 'No claimed device is available for this portal user.');
    test.skip(await mediaCheckbox.count() === 0, 'No uploaded media is available for this portal user.');

    const scheduleName = `E2E schedule ${runId}`;
    const scheduleDate = uniqueFutureDate();
    await page.locator('.schedule-mode-card').last().click();
    await page.locator('#scheduleCreateForm input[name="Name"]').fill(scheduleName);
    await page.locator('#startDate').fill(scheduleDate);
    await page.locator('#endDate').fill(scheduleDate);
    await page.locator('#startTime').fill('03:00');
    await page.locator('#endTime').fill('03:30');
    await page.locator('#scheduleCreateForm input[name="DeviceIds"]').first().check();
    await page.locator('#scheduleCreateForm input[name="MediaIds"]').first().check();
    await Promise.all([
      page.waitForURL(/\/portal\/schedules/),
      page.locator('#scheduleSubmitBtn').click(),
    ]);

    await expect(page.locator('.schedule-live-card', { hasText: scheduleName }).first()).toBeVisible({ timeout: 10_000 });
  });
});
