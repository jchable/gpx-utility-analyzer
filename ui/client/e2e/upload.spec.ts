import { test, expect } from '@playwright/test';
import { mockAllApi } from './helpers/mock-api';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));

test.describe('Upload Page', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApi(page);
  });

  test('renders page title and drop zone', async ({ page }) => {
    await page.goto('/upload');
    await expect(page.getByRole('heading', { name: 'Upload GPX Files' })).toBeVisible();
    await expect(page.getByText('Drag & drop GPX files here')).toBeVisible();
    await expect(page.getByText('or click to browse')).toBeVisible();
  });

  test('renders activity type selector with all types', async ({ page }) => {
    await page.goto('/upload');
    await expect(page.getByText('Activity Type')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Running' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Hiking' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Cycling' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Walking' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Swimming' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Other' })).toBeVisible();
  });

  test('file input accepts gpx files', async ({ page }) => {
    await page.goto('/upload');
    const fileInput = page.locator('input[type="file"]');
    await expect(fileInput).toHaveAttribute('accept', '.gpx');
  });

  test('adds file via file chooser', async ({ page }) => {
    await page.goto('/upload');
    const fixtureGpx = join(__dirname, 'fixtures', 'test.gpx');
    const fileInput = page.locator('input[type="file"]');
    await fileInput.setInputFiles(fixtureGpx);
    await expect(page.getByText('test.gpx')).toBeVisible();
    await expect(page.getByText('Ready')).toBeVisible();
  });

  test('removing a queued file mid-upload does not upload it or mislabel the next row', async ({ page }) => {
    const uploadedNames: string[] = [];
    let releaseFirst: () => void = () => {};
    const firstInFlight = new Promise<void>((r) => { releaseFirst = r; });

    // Registered after mockAllApi, so it wins (Playwright matches routes LIFO).
    await page.route('**/api/activities/upload', async (route) => {
      const post = route.request().postData() ?? '';
      const name = /filename="([^"]+)"/.exec(post)?.[1] ?? 'unknown';
      uploadedNames.push(name);
      if (uploadedNames.length === 1) await firstInFlight; // hold a.gpx in flight
      await route.fulfill({
        json: { id: `activity-${name.replace('.gpx', '')}`, name, status: 'Pending' },
      });
    });

    await page.goto('/upload');
    await page.setInputFiles(
      'input[type="file"]',
      ['a.gpx', 'b.gpx', 'c.gpx'].map((n) => ({
        name: n,
        mimeType: 'application/gpx+xml',
        buffer: Buffer.from('<gpx version="1.1"><trk><trkseg/></trk></gpx>'),
      })),
    );

    await page.getByRole('button', { name: /^Upload \d+ files?$/ }).click();

    // While a.gpx is in flight, the user changes their mind about b.gpx.
    const removeB = page.getByRole('button', { name: 'Remove b.gpx' });
    await expect(removeB).toBeVisible();
    await removeB.click();

    releaseFirst();
    await expect.poll(() => uploadedNames.length, { timeout: 10_000 }).toBeGreaterThanOrEqual(2);
    await page.waitForTimeout(500);

    // b.gpx must never reach the server, and c.gpx must — exactly once each.
    expect(uploadedNames).toEqual(['a.gpx', 'c.gpx']);

    // …and c.gpx's row must carry c.gpx's own activity, not b.gpx's.
    const rowC = page.locator('div.bg-surface-card').filter({ hasText: 'c.gpx' });
    await rowC.getByRole('button', { name: 'View' }).click();
    await expect(page).toHaveURL(/\/activities\/activity-c$/);
  });
});

test.describe('Upload Page — mobile layout', () => {
  test.use({ viewport: { width: 390, height: 844 } });

  test.beforeEach(async ({ page }) => {
    await mockAllApi(page);
  });

  test('drop zone renders on mobile', async ({ page }) => {
    await page.goto('/upload');
    await expect(page.getByText('Drag & drop GPX files here')).toBeVisible();
    await expect(page.getByText('Activity Type')).toBeVisible();
  });
});
