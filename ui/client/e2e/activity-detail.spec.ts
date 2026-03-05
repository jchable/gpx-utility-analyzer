import { test, expect } from '@playwright/test';
import { mockAllApi } from './helpers/mock-api';

test.describe('Activity Detail — Completed', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApi(page);
  });

  test('renders activity name and type badge', async ({ page }) => {
    await page.goto('/activities/act-1');
    await expect(page.getByRole('heading', { name: 'Morning Trail' })).toBeVisible();
    await expect(page.getByText('Completed').first()).toBeVisible();
  });

  test('renders performance stat gauges', async ({ page }) => {
    await page.goto('/activities/act-1');
    await page.waitForLoadState('networkidle');
    await expect(page.getByText('Performance Stats')).toBeVisible({ timeout: 10000 });
    await expect(page.getByText('Moving').first()).toBeVisible();
    await expect(page.getByText('Elevation').first()).toBeVisible();
  });

  test('renders extended stats', async ({ page }) => {
    await page.goto('/activities/act-1');
    await expect(page.getByText('Max Speed')).toBeVisible();
    await expect(page.getByText('Avg Pace')).toBeVisible();
    await expect(page.getByText('Max Elevation')).toBeVisible();
    await expect(page.getByText('Avg HR')).toBeVisible();
  });

  test('renders elevation chart container', async ({ page }) => {
    await page.goto('/activities/act-1');
    await expect(page.getByText('Elevation Profile')).toBeVisible();
  });

  test('renders AI report', async ({ page }) => {
    await page.goto('/activities/act-1');
    await expect(page.getByText('AI Analysis Report')).toBeVisible();
    await expect(page.getByText('Moderate (6/10)')).toBeVisible();
    await expect(page.getByText('Key Segments')).toBeVisible();
    await expect(page.getByText('Recommendations')).toBeVisible();
  });

  test('has action buttons', async ({ page }) => {
    await page.goto('/activities/act-1');
    await expect(page.locator('a', { hasText: 'GPX' })).toBeVisible();
    await expect(page.getByRole('button', { name: /Reanalyze/ })).toBeVisible();
    await expect(page.getByRole('button', { name: /Delete/ })).toBeVisible();
  });

  test('has map container', async ({ page }) => {
    await page.goto('/activities/act-1');
    const mapContainer = page.locator('[class*="maplibregl"], [class*="map-container"]').first();
    await expect(mapContainer).toBeVisible({ timeout: 10000 });
  });

  test('reanalyze button sends request', async ({ page }) => {
    const reanalyzePromise = page.waitForRequest('**/api/activities/act-1/reanalyze');
    await page.goto('/activities/act-1');
    await page.getByRole('button', { name: /Reanalyze/ }).click();
    const request = await reanalyzePromise;
    expect(request.method()).toBe('POST');
  });
});

test.describe('Activity Detail — mobile layout', () => {
  test.use({ viewport: { width: 390, height: 844 } });

  test.beforeEach(async ({ page }) => {
    await mockAllApi(page);
  });

  test('header and actions render on mobile', async ({ page }) => {
    await page.goto('/activities/act-1');
    await expect(page.getByRole('heading', { name: 'Morning Trail' })).toBeVisible();
    await expect(page.getByRole('button', { name: /Reanalyze/ })).toBeVisible();
    await expect(page.getByRole('button', { name: /Delete/ })).toBeVisible();
  });
});
