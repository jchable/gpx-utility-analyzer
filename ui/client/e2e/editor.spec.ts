import { test, expect } from '@playwright/test';
import { mockAllApi } from './helpers/mock-api';

test.describe('Editor Page — New Route', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApi(page);
  });

  test('displays editor page with toolbar', async ({ page }) => {
    await page.goto('/editor');
    // Toolbar buttons should be visible
    await expect(page.getByTitle(/Select|Sélectionner/i)).toBeVisible();
    await expect(page.getByTitle(/Add Point|Ajouter un point/i)).toBeVisible();
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
    await expect(page.getByText(/Discard|Abandonner/i).first()).toBeVisible();
  });
});

test.describe('Editor Page — Existing Route', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApi(page);
  });

  test('loads route data into editor', async ({ page }) => {
    await page.goto('/editor/route-1');
    // Route name should be filled from the loaded route
    const nameInput = page.locator('input[type="text"]').first();
    await expect(nameInput).toHaveValue('Col du Galibier', { timeout: 5000 });
  });

  test('shows elevation enrichment button for saved routes', async ({ page }) => {
    await page.goto('/editor/route-1');
    // Wait for route to load
    await expect(page.locator('input[type="text"]').first()).toHaveValue('Col du Galibier', { timeout: 5000 });
    // The enrichment button should be visible
    await expect(page.getByTitle(/Enrich elevation|Enrichir l'altitude/i)).toBeVisible();
  });
});

test.describe('Editor Page — Toolbar Modes', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApi(page);
  });

  test('toolbar has all mode buttons', async ({ page }) => {
    await page.goto('/editor');
    // Check all toolbar buttons are present
    await expect(page.getByTitle(/Select|Sélectionner/i)).toBeVisible();
    await expect(page.getByTitle(/Add Point|Ajouter un point/i)).toBeVisible();
    await expect(page.getByTitle(/Freehand|Dessin libre/i)).toBeVisible();
    await expect(page.getByTitle(/Split|Scinder/i)).toBeVisible();
    await expect(page.getByTitle(/Crop|Recadrer/i)).toBeVisible();
    await expect(page.getByTitle(/Add POI|Ajouter un POI/i)).toBeVisible();
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
