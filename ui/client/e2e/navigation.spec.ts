import { test, expect } from '@playwright/test';
import { mockAllApi } from './helpers/mock-api';

test.describe('Navigation — desktop', () => {
  test.use({ viewport: { width: 1280, height: 720 } });

  test.beforeEach(async ({ page }) => {
    await mockAllApi(page);
  });

  test('sidebar nav links navigate correctly', async ({ page }) => {
    await page.goto('/');
    const sidebar = page.locator('aside');

    await sidebar.getByRole('link', { name: /Activities/i }).click();
    await expect(page).toHaveURL(/\/activities/);

    await sidebar.getByRole('link', { name: /Upload/i }).click();
    await expect(page).toHaveURL(/\/upload/);

    await sidebar.getByRole('link', { name: /Settings/i }).click();
    await expect(page).toHaveURL(/\/settings/);

    await sidebar.getByRole('link', { name: /Integrations/i }).click();
    await expect(page).toHaveURL(/\/integrations/);

    await sidebar.getByRole('link', { name: /Dashboard/i }).click();
    await expect(page).toHaveURL('/');
  });

  test('sidebar shows language switcher', async ({ page }) => {
    await page.goto('/');
    // Language switcher shows current language code "EN"
    const sidebar = page.locator('aside');
    await expect(sidebar.getByText('EN')).toBeVisible();
  });
});

test.describe('Navigation — mobile', () => {
  test.use({ viewport: { width: 390, height: 844 } });

  test.beforeEach(async ({ page }) => {
    await mockAllApi(page);
  });

  test('bottom nav is visible', async ({ page }) => {
    await page.goto('/');
    const bottomNav = page.locator('nav.fixed.bottom-0');
    await expect(bottomNav).toBeVisible();
  });

  test('bottom nav links navigate correctly', async ({ page }) => {
    await page.goto('/');
    const bottomNav = page.locator('nav.fixed.bottom-0');
    await bottomNav.getByRole('link', { name: /Upload/i }).click();
    await expect(page).toHaveURL(/\/upload/);
  });
});

test.describe('Offline banner', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApi(page);
  });

  test('offline banner is hidden when online', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByText('You are offline')).not.toBeVisible();
  });
});
