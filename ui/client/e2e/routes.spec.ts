import { test, expect } from '@playwright/test';
import { mockAllApi } from './helpers/mock-api';

test.describe('Routes Page', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApi(page);
  });

  test('displays route list page with title', async ({ page }) => {
    await page.goto('/routes');
    await expect(page.getByRole('heading', { name: /My Routes|Mes Tracés/i })).toBeVisible();
  });

  test('shows route cards with name and stats', async ({ page }) => {
    await page.goto('/routes');
    await expect(page.getByText('Col du Galibier')).toBeVisible();
    await expect(page.getByText('Tour du Mont Blanc')).toBeVisible();
    await expect(page.getByText('Boucle VTT Forêt')).toBeVisible();
  });

  test('shows new route button', async ({ page }) => {
    await page.goto('/routes');
    await expect(page.getByRole('link', { name: /New Route|Nouveau Tracé/i })).toBeVisible();
  });

  test('shows import GPX button', async ({ page }) => {
    await page.goto('/routes');
    await expect(page.getByText(/Import GPX|Importer GPX/i)).toBeVisible();
  });

  test('navigates to editor when clicking new route', async ({ page }) => {
    await page.goto('/routes');
    await page.getByRole('link', { name: /New Route|Nouveau Tracé/i }).click();
    await expect(page).toHaveURL(/\/editor/);
  });
});
