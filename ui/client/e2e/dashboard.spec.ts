import { test, expect } from '@playwright/test';
import { mockAllApi } from './helpers/mock-api';

test.describe('Dashboard', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApi(page);
  });

  test('renders summary stat cards', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByText('Total Activities')).toBeVisible();
    await expect(page.getByText('42.5 km')).toBeVisible();
    await expect(page.getByText('1190 m')).toBeVisible();
  });

  test('renders recent activities', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByText('Morning Trail')).toBeVisible();
    await expect(page.getByText('Evening Run')).toBeVisible();
    await expect(page.getByText('Weekend Hike')).toBeVisible();
  });

  test('renders activity type breakdown', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByText('Activity Breakdown')).toBeVisible();
  });

  test('navigates to activity detail on click', async ({ page }) => {
    await page.goto('/');
    await page.getByText('Morning Trail').click();
    await expect(page).toHaveURL(/\/activities\/act-1/);
  });
});

test.describe('Dashboard — desktop layout', () => {
  test.use({ viewport: { width: 1280, height: 720 } });

  test.beforeEach(async ({ page }) => {
    await mockAllApi(page);
  });

  test('sidebar is visible', async ({ page }) => {
    await page.goto('/');
    const sidebar = page.locator('aside');
    await expect(sidebar).toBeVisible();
  });
});

test.describe('Dashboard — mobile layout', () => {
  test.use({ viewport: { width: 390, height: 844 } });

  test.beforeEach(async ({ page }) => {
    await mockAllApi(page);
  });

  test('bottom nav is visible', async ({ page }) => {
    await page.goto('/');
    const bottomNav = page.locator('nav.fixed.bottom-0');
    await expect(bottomNav).toBeVisible();
  });
});
