import { test, expect } from '@playwright/test';
import { mockAllApi } from './helpers/mock-api';

test.describe('Editor Page — New Route', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApi(page);
  });

  test('displays editor page with toolbar', async ({ page }) => {
    await page.goto('/editor');
    // Toolbar buttons should exist (on mobile they may be outside the visible scroll area)
    await expect(page.getByTitle(/Select|Sélectionner/i)).toHaveCount(1);
    await expect(page.getByTitle(/Add Point|Ajouter un point/i)).toHaveCount(1);
  });

  test('shows route name input', async ({ page }) => {
    await page.goto('/editor');
    const nameInput = page.locator('input[placeholder*="Route"]').first();
    await expect(nameInput).toBeVisible();
  });

  test('shows save and export buttons', async ({ page }) => {
    await page.goto('/editor');
    await expect(page.getByText(/Save|Enregistrer/i).first()).toBeVisible();
    await expect(page.getByText(/Export|Exporter/i).first()).toBeVisible();
  });

  test('shows map container', async ({ page }) => {
    await page.goto('/editor');
    // MapLibre map container
    const mapContainer = page.locator('[class*="maplibregl"], [class*="map-container"], .maplibregl-map').first();
    await expect(mapContainer).toBeVisible({ timeout: 10000 });
  });

  test('shows elevation profile section', async ({ page }) => {
    await page.goto('/editor');
    // Stats bar in elevation profile header
    await expect(page.getByText(/Distance|Distance/i).first()).toBeVisible();
  });

  test('shows back/discard button', async ({ page }) => {
    await page.goto('/editor');
    // On mobile the label text is hidden (hidden sm:inline), only the arrow icon is visible
    await expect(page.locator('button').filter({ has: page.locator('svg.lucide-arrow-left') }).first()).toBeVisible();
  });
});

test.describe('Editor Page — Existing Route', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApi(page);
  });

  test('loads route data into editor', async ({ page }) => {
    await page.goto('/editor/route-1');
    // Route name should be filled from the loaded route (input has no explicit type="text")
    const nameInput = page.locator('input[placeholder]').first();
    await expect(nameInput).toHaveValue('Col du Galibier', { timeout: 5000 });
  });

  test('shows elevation enrichment button for saved routes', async ({ page }) => {
    await page.goto('/editor/route-1');
    // Wait for route to load (input has no explicit type="text")
    await expect(page.locator('input[placeholder]').first()).toHaveValue('Col du Galibier', { timeout: 5000 });
    // The enrichment button should exist in the DOM
    await expect(page.getByTitle(/Enrich elevation|Enrichir l'altitude/i)).toHaveCount(1);
  });

  test('route export sends the bearer token instead of opening a bare tab', async ({ page }) => {
    const exportRequests: { url: string; auth: string | undefined }[] = [];
    await page.route('**/api/routes/*/export/*', async (route) => {
      exportRequests.push({
        url: route.request().url(),
        auth: route.request().headers()['authorization'],
      });
      await route.fulfill({
        status: 200,
        contentType: 'application/gpx+xml',
        body: '<gpx version="1.1"></gpx>',
      });
    });

    const popups: string[] = [];
    page.on('popup', (p) => popups.push(p.url()));

    await page.goto('/editor/route-1');
    await expect(page.locator('input[placeholder]').first()).toHaveValue('Col du Galibier', { timeout: 5000 });

    await page.getByRole('button', { name: /^(Export|Exporter)$/ }).click();
    await page.getByRole('button', { name: 'GPX', exact: true }).click();

    // Either the fetch landed (fixed) or a bare tab opened (broken).
    await expect.poll(() => exportRequests.length + popups.length).toBeGreaterThan(0);

    // A top-level navigation carries no Authorization header, so the export must
    // never be a popup.
    expect(popups).toHaveLength(0);
    expect(exportRequests).toHaveLength(1);
    expect(exportRequests[0].auth).toMatch(/^Bearer /);
  });
});

test.describe('Editor Page — Toolbar Modes', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApi(page);
  });

  test('toolbar has all mode buttons', async ({ page }) => {
    await page.goto('/editor');
    // Check all toolbar buttons exist (on mobile the toolbar may scroll outside viewport)
    await expect(page.getByTitle(/Select|Sélectionner/i)).toHaveCount(1);
    await expect(page.getByTitle(/Add Point|Ajouter un point/i)).toHaveCount(1);
    await expect(page.getByTitle(/Freehand|Dessin libre/i)).toHaveCount(1);
    await expect(page.getByTitle(/Split|Scinder/i)).toHaveCount(1);
    await expect(page.getByTitle(/Crop|Recadrer/i)).toHaveCount(1);
    // Use anchored regex to avoid matching "Add Point" with "Add POI"
    await expect(page.getByTitle(/^Add POI$|^Ajouter un POI$/i)).toHaveCount(1);
  });

  test('toolbar has undo/redo buttons', async ({ page }) => {
    await page.goto('/editor');
    await expect(page.getByTitle(/Undo|Annuler/i)).toBeVisible();
    await expect(page.getByTitle(/Redo|Rétablir/i)).toBeVisible();
  });

  test('toolbar has reverse button', async ({ page }) => {
    await page.goto('/editor');
    await expect(page.getByTitle(/Reverse|Inverser/i)).toBeVisible();
  });
});
