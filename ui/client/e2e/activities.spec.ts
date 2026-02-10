import { test, expect } from '@playwright/test';
import { mockAllApi } from './helpers/mock-api';

test.describe('Activity List', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApi(page);
  });

  test('renders activity cards', async ({ page }) => {
    await page.goto('/activities');
    await expect(page.getByText('Morning Trail')).toBeVisible();
    await expect(page.getByText('Evening Run')).toBeVisible();
    await expect(page.getByText('Weekend Hike')).toBeVisible();
    await expect(page.getByText('City Cycle')).toBeVisible();
  });

  test('displays activity type badges on cards', async ({ page }) => {
    await page.goto('/activities');
    // Type badges are <span> elements — exclude <option> in the filter <select>
    await expect(page.locator('span').filter({ hasText: /^Running$/ }).first()).toBeVisible();
    await expect(page.locator('span').filter({ hasText: /^Hiking$/ }).first()).toBeVisible();
    await expect(page.locator('span').filter({ hasText: /^Cycling$/ }).first()).toBeVisible();
  });

  test('displays status indicators', async ({ page }) => {
    await page.goto('/activities');
    await expect(page.getByText('Completed').first()).toBeVisible();
    await expect(page.getByText('Analyzing GPX')).toBeVisible();
  });

  test('has type filter dropdown', async ({ page }) => {
    await page.goto('/activities');
    const select = page.locator('select');
    await expect(select).toBeVisible();
    await expect(select.locator('option').first()).toHaveText('All Types');
  });

  test('has pagination controls', async ({ page }) => {
    await page.goto('/activities');
    await expect(page.getByRole('button', { name: 'Previous' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Next' })).toBeVisible();
    await expect(page.getByText('Page 1')).toBeVisible();
  });

  test('navigates to activity detail on click', async ({ page }) => {
    await page.goto('/activities');
    await page.getByText('Morning Trail').click();
    await expect(page).toHaveURL(/\/activities\/act-1/);
  });
});
