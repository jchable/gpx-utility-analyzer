import { test, expect } from '@playwright/test';

test.describe('PWA meta tags and assets', () => {
  test('title is GPX Analyzer', async ({ page }) => {
    await page.goto('/');
    await expect(page).toHaveTitle('GPX Analyzer');
  });

  test('has theme-color meta tag', async ({ page }) => {
    await page.goto('/');
    const meta = page.locator('meta[name="theme-color"]');
    const content = await meta.getAttribute('content');
    expect(['#0f0f1a', '#f0f2f5']).toContain(content);
  });

  test('has manifest link', async ({ page }) => {
    await page.goto('/');
    const link = page.locator('link[rel="manifest"]');
    await expect(link).toHaveAttribute('href', '/manifest.webmanifest');
  });

  test('has apple-touch-icon', async ({ page }) => {
    await page.goto('/');
    const link = page.locator('link[rel="apple-touch-icon"]');
    await expect(link).toHaveCount(1);
  });

  test('manifest.webmanifest is accessible and valid', async ({ request }) => {
    const response = await request.get('/manifest.webmanifest');
    expect(response.ok()).toBeTruthy();
    const manifest = await response.json();
    expect(manifest.name).toBe('GPX Analyzer');
    expect(manifest.display).toBe('standalone');
    expect(manifest.theme_color).toBe('#0f0f1a');
    expect(manifest.icons).toBeDefined();
    expect(manifest.icons.length).toBeGreaterThanOrEqual(2);
  });

  test('service worker is accessible', async ({ request }) => {
    const response = await request.get('/sw.js');
    expect(response.ok()).toBeTruthy();
  });

  test('favicon.svg is accessible', async ({ request }) => {
    const response = await request.get('/favicon.svg');
    expect(response.ok()).toBeTruthy();
  });
});
