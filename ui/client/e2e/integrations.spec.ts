import { test, expect } from '@playwright/test';
import { mockAllApi } from './helpers/mock-api';

test.describe('Integrations Page', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApi(page);
  });

  test('renders page heading', async ({ page }) => {
    await page.goto('/integrations');
    await expect(page.locator('h1').filter({ hasText: 'Integrations' })).toBeVisible();
    await expect(page.getByText('Connect your favorite sports platforms')).toBeVisible();
  });

  test('shows connected provider with status', async ({ page }) => {
    await page.goto('/integrations');
    await expect(page.getByText('Strava').first()).toBeVisible();
    await expect(page.getByText('Connected').first()).toBeVisible();
    await expect(page.getByRole('button', { name: 'Disconnect' })).toBeVisible();
  });

  test('shows connect button for disconnected provider', async ({ page }) => {
    await page.goto('/integrations');
    await expect(page.getByText('Garmin Connect')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Connect', exact: true })).toBeVisible();
  });
});
