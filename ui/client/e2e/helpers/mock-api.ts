import { Page } from '@playwright/test';
import { readFileSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const fixturesDir = join(__dirname, '..', 'fixtures');

function loadFixture(name: string) {
  return JSON.parse(readFileSync(join(fixturesDir, name), 'utf-8'));
}

const dashboard = loadFixture('dashboard.json');
const activities = loadFixture('activities.json');
const activityCompleted = loadFixture('activity-completed.json');
const activityAnalyzing = loadFixture('activity-analyzing.json');
const profile = loadFixture('profile.json');
const userProfile = loadFixture('user-profile.json');
const track = loadFixture('track.json');
const integrations = loadFixture('integrations.json');
const settings = loadFixture('settings.json');
const providers = loadFixture('providers.json');
const routes = loadFixture('routes.json');
const routeDetail = loadFixture('route-detail.json');

// Build a fake JWT that jwt-decode can parse (not cryptographically valid, just base64-decodable)
function fakeMockJwt(): string {
  const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
  const payload = btoa(
    JSON.stringify({
      sub: 'test-user-id',
      email: 'test@gpx-analyzer.test',
      exp: Math.floor(Date.now() / 1000) + 3600, // 1h from now
    }),
  );
  const sig = btoa('fake-signature');
  return `${header}.${payload}.${sig}`;
}

export async function mockAllApi(page: Page) {
  // Seed localStorage with fake auth tokens BEFORE any navigation
  const fakeJwt = fakeMockJwt();
  await page.addInitScript(
    (tokens: { jwt: string }) => {
      localStorage.setItem('gpx_access_token', tokens.jwt);
      localStorage.setItem('gpx_refresh_token', 'mock-refresh-token');
    },
    { jwt: fakeJwt },
  );

  // Auth — mock /api/auth/me to simulate authenticated user
  await page.route('**/api/auth/me', (route) =>
    route.fulfill({
      json: {
        id: 'test-user-id',
        email: 'test@gpx-analyzer.test',
        displayName: 'Test User',
        role: 'Admin',
      },
    }),
  );

  // Auth — mock refresh token
  await page.route('**/api/auth/refresh', (route) =>
    route.fulfill({
      json: {
        accessToken: fakeJwt,
        refreshToken: 'mock-refresh-token',
        expiresAt: new Date(Date.now() + 3600000).toISOString(),
      },
    }),
  );

  // Auth — mock logout
  await page.route('**/api/auth/logout', (route) =>
    route.fulfill({ status: 204 }),
  );

  // User profile (GET and PUT)
  await page.route('**/api/profile', (route) => {
    if (route.request().method() === 'GET') {
      route.fulfill({ json: userProfile });
    } else {
      route.fulfill({ json: userProfile });
    }
  });

  // Change password
  await page.route('**/api/profile/change-password', (route) =>
    route.fulfill({ status: 204 }),
  );

  // Dashboard
  await page.route('**/api/dashboard/summary', (route) =>
    route.fulfill({ json: dashboard }),
  );

  // Activities list
  await page.route('**/api/activities?*', (route) =>
    route.fulfill({ json: activities }),
  );

  // Activity detail — completed
  await page.route('**/api/activities/act-1', (route) =>
    route.fulfill({ json: activityCompleted }),
  );

  // Activity detail — analyzing
  await page.route('**/api/activities/act-5', (route) =>
    route.fulfill({ json: activityAnalyzing }),
  );

  // Elevation profile
  await page.route('**/api/activities/act-1/profile', (route) =>
    route.fulfill({ json: profile }),
  );

  // Track GeoJSON
  await page.route('**/api/activities/act-1/track', (route) =>
    route.fulfill({ json: track }),
  );

  // GPX download
  await page.route('**/api/activities/act-1/gpx', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/gpx+xml',
      body: '<gpx></gpx>',
    }),
  );

  // Integrations
  await page.route('**/api/integrations', (route) =>
    route.fulfill({ json: integrations }),
  );

  // Settings (GET and PUT)
  await page.route('**/api/settings', (route) => {
    if (route.request().method() === 'GET') {
      route.fulfill({ json: settings });
    } else {
      route.fulfill({ status: 200, json: settings });
    }
  });

  // AI providers list
  await page.route('**/api/settings/providers', (route) =>
    route.fulfill({ json: providers }),
  );

  // Upload — returns a completed activity
  await page.route('**/api/activities/upload', (route) =>
    route.fulfill({ json: activityCompleted }),
  );

  // Reanalyze
  await page.route('**/api/activities/*/reanalyze', (route) =>
    route.fulfill({ status: 200 }),
  );

  // Delete
  await page.route('**/api/activities/*', (route) => {
    if (route.request().method() === 'DELETE') {
      route.fulfill({ status: 200 });
    } else {
      // Fall through to other handlers
      route.fallback();
    }
  });

  // --- Routes endpoints ---

  // Routes list
  await page.route('**/api/routes?*', (route) =>
    route.fulfill({ json: routes }),
  );

  // Route tags
  await page.route('**/api/routes/tags', (route) =>
    route.fulfill({ json: ['alps', 'mountain', 'tmb', 'forest', 'mtb'] }),
  );

  // Route detail
  await page.route('**/api/routes/route-1', (route) => {
    if (route.request().method() === 'DELETE') {
      route.fulfill({ status: 204 });
    } else if (route.request().method() === 'PUT') {
      route.fulfill({ json: routeDetail });
    } else {
      route.fulfill({ json: routeDetail });
    }
  });

  // Autosave
  await page.route('**/api/routes/*/autosave', (route) =>
    route.fulfill({ status: 204 }),
  );

  // Create route
  await page.route('**/api/routes', (route) => {
    if (route.request().method() === 'POST') {
      route.fulfill({ json: routeDetail });
    } else {
      route.fallback();
    }
  });

  // Create from activity
  await page.route('**/api/routes/from-activity/*', (route) =>
    route.fulfill({ json: routeDetail }),
  );

  // Import GPX
  await page.route('**/api/routes/import', (route) =>
    route.fulfill({ json: routeDetail }),
  );

  // Export endpoints
  await page.route('**/api/routes/*/export/*', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/octet-stream',
      body: '<gpx></gpx>',
    }),
  );

  // Routing preview
  await page.route('**/api/routes/routing/preview', (route) =>
    route.fulfill({
      json: {
        coordinates: [[6.4, 45.06, 1450], [6.42, 45.07, 1700], [6.46, 45.064, 2642]],
        distanceMeters: 18500,
        durationSeconds: 14400,
      },
    }),
  );

  // Elevation enrichment
  await page.route('**/api/routes/*/elevation', (route) =>
    route.fulfill({ json: routeDetail }),
  );

  // Route deletion
  await page.route('**/api/routes/*', (route) => {
    if (route.request().method() === 'DELETE') {
      route.fulfill({ status: 204 });
    } else {
      route.fallback();
    }
  });
}
