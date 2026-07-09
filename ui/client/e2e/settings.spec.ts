import { test, expect } from '@playwright/test';
import { mockAllApi } from './helpers/mock-api';

// NOTE: The Settings page was restructured. Integration "Client ID / Client Secret"
// credential inputs were replaced by OAuth connect/disconnect cards, and there is no
// longer an "AI Provider" section on this page. Assertions below target the real
// current UI. Section titles / button labels come from the `settings` namespace and
// are matched with case-insensitive regexes so the tests stay green regardless of the
// exact rendered casing; the integration card labels (Strava / Garmin Connect /
// Connected) come from the `integrations` namespace, and the form values come from the
// settings fixture (e2e/fixtures/settings.json).

test.describe('Settings Page', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApi(page);
  });

  test('renders all settings sections', async ({ page }) => {
    await page.goto('/settings');
    const main = page.locator('main');
    // Page title heading (settings:title)
    await expect(main.getByRole('heading', { level: 1 })).toBeVisible();
    // Two section cards render: Integrations + Analysis Preferences
    await expect(main.getByRole('heading', { name: /integrations/i })).toBeVisible();
    await expect(main.getByRole('heading', { name: /analysis\s*preferences/i })).toBeVisible();
    await expect(main.getByRole('heading', { level: 2 })).toHaveCount(2);
  });

  test('renders integration credential fields', async ({ page }) => {
    await page.goto('/settings');
    const main = page.locator('main');
    // OAuth connect/disconnect cards replaced the old Client ID / Client Secret inputs.
    // Provider names come from the integrations namespace (integrations.json).
    await expect(main.getByText('Strava', { exact: true }).first()).toBeVisible();
    await expect(main.getByText('Garmin Connect', { exact: true })).toBeVisible();
    // Strava is connected in the fixture -> "Connected" badge + a disconnect action.
    await expect(main.getByText('Connected', { exact: true })).toBeVisible();
    await expect(main.getByRole('button', { name: /^disconnect$/i })).toBeVisible();
    // Garmin is not connected in the fixture -> a connect action.
    await expect(main.getByRole('button', { name: /^connect$/i })).toBeVisible();
  });

  test('has save button', async ({ page }) => {
    await page.goto('/settings');
    const main = page.locator('main');
    await expect(main.getByRole('button', { name: /save\s*settings/i })).toBeVisible();
  });

  test('form fields are populated from settings', async ({ page }) => {
    await page.goto('/settings');
    const main = page.locator('main');
    // The Analysis Preferences selects are populated from the settings fixture.
    const selects = main.locator('select');
    await expect(selects).toHaveCount(4);
    await expect(selects.nth(0)).toHaveValue('trail'); // analysis.preset
    await expect(selects.nth(1)).toHaveValue('medium'); // analysis.smoothing
    await expect(selects.nth(2)).toHaveValue('light'); // analysis.trackSmoothing
    await expect(selects.nth(3)).toHaveValue('threshold'); // analysis.elevationAlgorithm
    // The two analysis toggles render as switches, reflecting the fixture (both false).
    await expect(main.getByRole('switch')).toHaveCount(2);
    await expect(main.getByRole('switch').first()).toHaveAttribute('aria-checked', 'false');
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
    const main = page.locator('main');
    await expect(main.getByRole('heading', { level: 1 })).toBeVisible();
    await expect(main.getByRole('heading', { name: /integrations/i })).toBeVisible();
    await expect(main.getByRole('heading', { name: /analysis\s*preferences/i })).toBeVisible();
    await expect(main.getByRole('button', { name: /save\s*settings/i })).toBeVisible();
  });
});
