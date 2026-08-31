import { test, expect } from '@playwright/test';
import { mockAllApi } from './helpers/mock-api';

test.describe('race plan detail', () => {
  test.beforeEach(async ({ page }) => {
    await mockAllApi(page);
  });

  test('compute-times PUT carries the whole plan, not a 4-field payload', async ({ page }) => {
    const puts: Record<string, unknown>[] = [];
    await page.route('**/api/race-plans/plan-1', async (route) => {
      if (route.request().method() === 'PUT') {
        puts.push(route.request().postDataJSON());
      }
      await route.fallback();
    });

    await page.goto('/race-plans/plan-1');
    await expect(page.getByRole('heading', { name: 'UTMB 2026' })).toBeVisible();

    // The coefficient must actually change for handleComputeTimes to issue the PUT.
    await page.getByRole('button', { name: 'Elite', exact: true }).click();
    await page.getByRole('button', { name: /compute times|calculer les temps/i }).click();
    await expect.poll(() => puts.length).toBeGreaterThan(0);

    const body = puts[0];
    // The API's PUT is a full replace: anything missing here is nulled server-side.
    expect(body).toHaveProperty('raceDate', '2026-06-06');
    expect(body).toHaveProperty('startTime', '04:00');
    expect(body).toHaveProperty('startLatitude');
    expect(body.startLatitude).not.toBeNull();
    expect(body).toHaveProperty('startLongitude');
    expect(body.startLongitude).not.toBeNull();
    expect(body).toHaveProperty('targetTimeSeconds', 144000);
    expect(body).toHaveProperty('targetTimeBSeconds');
    expect(body.targetTimeBSeconds).not.toBeNull();
    expect(body).toHaveProperty('targetTimeCSeconds');
    expect(body.targetTimeCSeconds).not.toBeNull();
    expect(body).toHaveProperty('sweatRateMLPerHour', 700);
    // The delta the caller actually intended.
    expect(body).toHaveProperty('performanceCoefficient', 0.95);
  });

  test('meta-form save PUT carries the whole plan, not a 7-field payload', async ({ page }) => {
    const puts: Record<string, unknown>[] = [];
    await page.route('**/api/race-plans/plan-1', async (route) => {
      if (route.request().method() === 'PUT') {
        puts.push(route.request().postDataJSON());
      }
      await route.fallback();
    });

    await page.goto('/race-plans/plan-1');
    await expect(page.getByRole('heading', { name: 'UTMB 2026' })).toBeVisible();

    // Open the meta form (collapsed behind a dashed placeholder button) and save it.
    await page.getByRole('button', { name: /race date.*start time|date de course.*heure de départ/i }).click();
    await page.getByRole('button', { name: /^(save|enregistrer)$/i }).click();
    await expect.poll(() => puts.length).toBeGreaterThan(0);

    const body = puts[0];
    expect(body).toHaveProperty('startLatitude');
    expect(body.startLatitude).not.toBeNull();
    expect(body).toHaveProperty('startLongitude');
    expect(body.startLongitude).not.toBeNull();
    expect(body).toHaveProperty('targetTimeBSeconds');
    expect(body.targetTimeBSeconds).not.toBeNull();
    expect(body).toHaveProperty('targetTimeCSeconds');
    expect(body.targetTimeCSeconds).not.toBeNull();
    expect(body).toHaveProperty('sweatRateMLPerHour', 700);
    expect(body).toHaveProperty('description');
    expect(body.description).not.toBeNull();
    expect(body).toHaveProperty('equipment');
    expect(body.equipment).not.toBeNull();
  });
});
