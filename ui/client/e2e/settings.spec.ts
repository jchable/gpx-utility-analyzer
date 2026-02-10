import { test, expect } from '@playwright/test';
import { mockAllApi } from './helpers/mock-api';

test.describe('Settings Page', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApi(page);
  });

  test('renders all settings sections', async ({ page }) => {
    await page.goto('/settings');
    await expect(page.getByRole('heading', { name: 'Settings', exact: true })).toBeVisible();
    await expect(page.getByText('Integration Credentials')).toBeVisible();
    await expect(page.getByText('Analysis Preferences')).toBeVisible();
    await expect(page.getByText('AI Provider').first()).toBeVisible();
  });

  test('renders integration credential fields', async ({ page }) => {
    await page.goto('/settings');
    await expect(page.getByText('Strava').first()).toBeVisible();
    await expect(page.getByText('Garmin Connect')).toBeVisible();
    await expect(page.getByText('Client ID')).toBeVisible();
    await expect(page.getByText('Client Secret')).toBeVisible();
  });

  test('has save button', async ({ page }) => {
    await page.goto('/settings');
    await expect(page.getByRole('button', { name: 'Save Settings' })).toBeVisible();
  });

  test('form fields are populated from settings', async ({ page }) => {
    await page.goto('/settings');
    const stravaInput = page.locator('input[placeholder="Enter Strava Client ID"]');
    await expect(stravaInput).toHaveValue('12345');
  });
});

test.describe('Settings Page — desktop grids', () => {
  test.use({ viewport: { width: 1280, height: 720 } });

  test.beforeEach(async ({ page }) => {
    await mockAllApi(page);
  });

  test('grid containers are present', async ({ page }) => {
    await page.goto('/settings');
    const grids = page.locator('.grid');
    await expect(grids.first()).toBeVisible();
  });
});

test.describe('Settings Page — mobile layout', () => {
  test.use({ viewport: { width: 390, height: 844 } });

  test.beforeEach(async ({ page }) => {
    await mockAllApi(page);
  });

  test('all sections render on mobile', async ({ page }) => {
    await page.goto('/settings');
    await expect(page.getByRole('heading', { name: 'Settings', exact: true })).toBeVisible();
    await expect(page.getByText('Integration Credentials')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Save Settings' })).toBeVisible();
  });
});
