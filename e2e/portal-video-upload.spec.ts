import { expect, test } from '@playwright/test';

const username = process.env.E2E_USER ?? '';
const password = process.env.E2E_PASSWORD ?? '';
const sampleVideo = process.env.E2E_SAMPLE_VIDEO ?? '';

test('login page loads and allows browser video blobs for thumbnails', async ({ page, baseURL }) => {
  const response = await page.goto('/account/login');

  expect(response?.ok()).toBeTruthy();
  expect(response?.headers()['content-security-policy']).toContain("media-src 'self' blob:");
  await expect(page.locator('#loginUsername')).toBeVisible();
  await expect(page.locator('#loginPassword')).toBeVisible();
});

test('uploads a video and renders a stored thumbnail', async ({ page }) => {
  test.skip(!username || !password || !sampleVideo, 'Set E2E_USER, E2E_PASSWORD, and E2E_SAMPLE_VIDEO to run upload E2E.');

  await page.goto('/account/login');
  await page.locator('#loginUsername').fill(username);
  await page.locator('#loginPassword').fill(password);
  await page.locator('button[type="submit"]').click();
  await page.waitForURL(/\/portal\/dashboard|\/admin|\/portal\/videos/);

  await page.goto('/portal/videos');
  await expect(page.locator('#videoFile')).toBeAttached();

  const uploadResponse = page.waitForResponse((response) =>
    response.url().includes('/api/portal/upload') && response.request().method() === 'POST'
  );

  await page.locator('#videoFile').setInputFiles(sampleVideo);
  const response = await uploadResponse;
  expect(response.ok()).toBeTruthy();
  const payload = await response.json();
  expect(payload.thumbnailUrl).toMatch(/^\/uploads\/.+\.thumb\.jpg$/);

  const thumbnail = page.locator(`img[src="${payload.thumbnailUrl}"]`).first();
  await expect(thumbnail).toBeVisible();
});
