import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { api, tryRefreshToken, __resetRefreshStateForTests } from './client';

describe('tryRefreshToken single-flight', () => {
  beforeEach(() => {
    localStorage.clear();
    __resetRefreshStateForTests();
    localStorage.setItem('gpx_access_token', 'stale-access');
    localStorage.setItem('gpx_refresh_token', 'refresh-1');
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('issues exactly one refresh request for five concurrent callers', async () => {
    let calls = 0;
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        expect(url).toContain('/auth/refresh');
        calls += 1;
        // The API rotates single-use refresh tokens: only the first
        // presentation of refresh-1 succeeds.
        if (calls > 1) {
          return new Response(JSON.stringify({ code: 'INVALID_REFRESH_TOKEN' }), { status: 401 });
        }
        return new Response(
          JSON.stringify({ accessToken: 'fresh-access', refreshToken: 'refresh-2' }),
          { status: 200, headers: { 'Content-Type': 'application/json' } },
        );
      }),
    );

    const results = await Promise.all([
      tryRefreshToken(), tryRefreshToken(), tryRefreshToken(),
      tryRefreshToken(), tryRefreshToken(),
    ]);

    expect(calls).toBe(1);
    expect(results).toEqual([true, true, true, true, true]);
    expect(localStorage.getItem('gpx_access_token')).toBe('fresh-access');
    expect(localStorage.getItem('gpx_refresh_token')).toBe('refresh-2');
  });

  it('allows a new refresh after the in-flight one settles', async () => {
    let calls = 0;
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        calls += 1;
        return new Response(
          JSON.stringify({ accessToken: `access-${calls}`, refreshToken: `refresh-${calls + 1}` }),
          { status: 200, headers: { 'Content-Type': 'application/json' } },
        );
      }),
    );

    expect(await tryRefreshToken()).toBe(true);
    expect(await tryRefreshToken()).toBe(true);
    expect(calls).toBe(2);
  });

  it('retries a 401 exactly once and does not loop when the retry 401s too', async () => {
    let refreshCalls = 0;
    let dataCalls = 0;
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        if (url.includes('/auth/refresh')) {
          refreshCalls += 1;
          return new Response(
            JSON.stringify({ accessToken: 'fresh-access', refreshToken: 'refresh-2' }),
            { status: 200, headers: { 'Content-Type': 'application/json' } },
          );
        }
        // The endpoint stays 401 even with a brand-new access token.
        dataCalls += 1;
        return new Response(JSON.stringify({ code: 'UNAUTHORIZED' }), { status: 401 });
      }),
    );

    await expect(api.getDashboardSummary()).rejects.toThrow();

    // Original request + exactly one retry, and exactly one refresh.
    expect(dataCalls).toBe(2);
    expect(refreshCalls).toBe(1);
  });

  it('does not restore tokens when logout occurs while refresh is in flight', async () => {
    let releaseResponse!: () => void;
    const responseGate = new Promise<void>((resolve) => { releaseResponse = resolve; });
    vi.stubGlobal('fetch', vi.fn(async () => {
      await responseGate;
      return new Response(
        JSON.stringify({ accessToken: 'obsolete-access', refreshToken: 'obsolete-refresh' }),
        { status: 200, headers: { 'Content-Type': 'application/json' } },
      );
    }));

    const refresh = tryRefreshToken();
    localStorage.removeItem('gpx_access_token');
    localStorage.removeItem('gpx_refresh_token');
    releaseResponse();

    await expect(refresh).resolves.toBe(false);
    expect(localStorage.getItem('gpx_access_token')).toBeNull();
    expect(localStorage.getItem('gpx_refresh_token')).toBeNull();
  });
});
