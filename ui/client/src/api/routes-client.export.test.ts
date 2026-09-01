import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { routesApi } from './routes-client';

/**
 * The export goes through fetch + a blob URL because auth is bearer-only. Two things
 * about that path are browser-sensitive and were wrong: Firefox ignores a programmatic
 * click on an anchor that is not in the document, and revoking the object URL in the
 * same tick can cancel the download the click just started.
 */
describe('routesApi.exportRoute', () => {
  const originalCreateObjectURL = URL.createObjectURL;
  const originalRevokeObjectURL = URL.revokeObjectURL;

  beforeEach(() => {
    vi.useFakeTimers();
    document.body.innerHTML = '';
    localStorage.clear();
    localStorage.setItem('gpx_access_token', 'token-1');
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response('<gpx/>', { status: 200 })),
    );
    // jsdom implements neither, so they are installed rather than spied on — and
    // restored in afterEach so the mutation does not outlive this file.
    URL.createObjectURL = vi.fn(() => 'blob:fake');
    URL.revokeObjectURL = vi.fn();
  });

  afterEach(() => {
    URL.createObjectURL = originalCreateObjectURL;
    URL.revokeObjectURL = originalRevokeObjectURL;
    vi.useRealTimers();
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('clicks an anchor that is attached to the document', async () => {
    let attachedAtClick = false;
    const clickSpy = vi
      .spyOn(HTMLAnchorElement.prototype, 'click')
      .mockImplementation(function (this: HTMLAnchorElement) {
        attachedAtClick = this.isConnected;
      });

    await routesApi.exportRoute('r1', 'gpx');

    expect(clickSpy).toHaveBeenCalledOnce();
    expect(attachedAtClick).toBe(true);
  });

  it('does not revoke the blob url in the same tick as the click', async () => {
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});

    await routesApi.exportRoute('r1', 'gpx');

    expect(URL.revokeObjectURL).not.toHaveBeenCalled();

    vi.runAllTimers();
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:fake');
    expect(document.querySelector('a[download]')).toBeNull();
  });

  it('names the file from the caller, falling back to the route id', async () => {
    const names: string[] = [];
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (
      this: HTMLAnchorElement,
    ) {
      names.push(this.download);
    });

    await routesApi.exportRoute('r1', 'gpx', 'my-route.gpx');
    await routesApi.exportRoute('r2', 'geojson');

    expect(names).toEqual(['my-route.gpx', 'route-r2.geojson']);
  });
});
