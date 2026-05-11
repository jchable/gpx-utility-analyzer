import { test, expect } from '@playwright/test';
import { mockAllApi } from './helpers/mock-api';

// Helper to click the language switcher, handling sidebar (desktop) vs bottom nav (mobile)
async function clickLanguageSwitcher(page: import('@playwright/test').Page) {
  // Wait for Suspense to resolve (i18n translation files loading)
  await page.waitForLoadState('networkidle');
  const sidebar = page.locator('aside');
  if (await sidebar.isVisible()) {
    // Desktop: sidebar language button — use dispatchEvent to work around
    // Playwright actionability false-negative in app-shell flex layout
    await sidebar.locator('button[title*="Switch to FR"]').dispatchEvent('click');
  } else {
    // Mobile: bottom nav language button — dispatchEvent to work around viewport overflow
    await page.locator('nav.fixed button[title*="Switch to FR"]').dispatchEvent('click');
  }
}

test.describe('Internationalization', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApi(page);
    // Ensure English by clearing stored language before navigation
    await page.addInitScript(() => {
      localStorage.removeItem('i18nextLng');
    });
  });

  test('default language is English', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByText('Your activity overview at a glance')).toBeVisible();
    await expect(page.getByText('Recent Activities')).toBeVisible();
  });

  test('switch to French updates page text', async ({ page }) => {
    await page.goto('/');
    await clickLanguageSwitcher(page);
    await expect(page.getByRole('heading', { name: 'Tableau de bord' })).toBeVisible({ timeout: 5000 });
  });

  test('activity type abbreviations visible', async ({ page }) => {
    await page.goto('/');
    // Recent activities show 3-char uppercase abbreviation of type
    await expect(page.getByText('TRA').first()).toBeVisible();
  });

  test('switch language and navigate preserves language', async ({ page }) => {
    await page.goto('/');
    await clickLanguageSwitcher(page);
    await expect(page.getByRole('heading', { name: 'Tableau de bord' })).toBeVisible({ timeout: 5000 });

    // Navigate to activities — should stay in French
    const activitiesLink = (await page.locator('aside').isVisible())
      ? page.locator('aside').getByRole('link', { name: /Activités/i })
      : page.locator('nav').getByRole('link', { name: /Activités/i }).first();
    await activitiesLink.click();
    await expect(page.getByRole('heading', { name: 'Activités', exact: true })).toBeVisible();
  });
});
