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
