import i18n from '../i18n';
import type {
  RouteListItem,
  RouteDetail,
  RouteCreateRequest,
  RouteUpdateRequest,
  RouteAutoSaveRequest,
} from '../types/route';

const BASE = '/api';
const TOKEN_KEY = 'gpx_access_token';

function langHeaders(): Record<string, string> {
  return { 'Accept-Language': i18n.language || 'en' };
}

function authHeaders(): Record<string, string> {
  const token = localStorage.getItem(TOKEN_KEY);
  return token ? { Authorization: `Bearer ${token}` } : {};
}

function allHeaders(): Record<string, string> {
  return { ...langHeaders(), ...authHeaders() };
}

async function fetchJson<T>(url: string, init?: RequestInit): Promise<T> {
  const headers = { ...allHeaders(), ...init?.headers };
  const res = await fetch(`${BASE}${url}`, { cache: 'no-cache', ...init, headers });
  if (!res.ok) {
    let code = '';
    try {
      const json = await res.json();
      code = json.code || '';
    } catch { /* not JSON */ }
    if (code) throw new Error(code);
    throw new Error(`API error ${res.status}`);
  }
  return res.json();
}

export const routesApi = {
  getRoutes: (page = 1, pageSize = 20, type?: string, status?: string) => {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (type) params.set('type', type);
    if (status) params.set('status', status);
    return fetchJson<RouteListItem[]>(`/routes?${params}`);
  },

  getRoute: (id: string) => fetchJson<RouteDetail>(`/routes/${id}`),

  createRoute: (data: RouteCreateRequest) =>
    fetchJson<RouteDetail>('/routes', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data),
    }),

  updateRoute: (id: string, data: RouteUpdateRequest) =>
    fetchJson<RouteDetail>(`/routes/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data),
    }),

  autoSaveRoute: async (id: string, data: RouteAutoSaveRequest): Promise<void> => {
    const res = await fetch(`${BASE}/routes/${id}/autosave`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json', ...allHeaders() },
      body: JSON.stringify(data),
    });
    if (!res.ok) throw new Error(`Auto-save failed: ${res.status}`);
  },

  deleteRoute: async (id: string): Promise<void> => {
    const res = await fetch(`${BASE}/routes/${id}`, { method: 'DELETE', headers: allHeaders() });
    if (!res.ok) throw new Error(`API error ${res.status}`);
  },

  createFromActivity: (activityId: string) =>
    fetchJson<RouteDetail>(`/routes/from-activity/${activityId}`, { method: 'POST' }),

  importGpx: async (file: File): Promise<RouteDetail> => {
    const formData = new FormData();
    formData.append('file', file);
    const res = await fetch(`${BASE}/routes/import`, {
      method: 'POST',
      headers: allHeaders(),
      body: formData,
    });
    if (!res.ok) {
      let code = '';
      try {
        const json = await res.json();
        code = json.code || '';
      } catch { /* not JSON */ }
      if (code) throw new Error(code);
      throw new Error(`Import failed: ${res.status}`);
    }
    return res.json();
  },

  getTags: () => fetchJson<string[]>('/routes/tags'),

  // --- Export (server-side) ---
  /**
   * Downloads a route export. Auth is bearer-only, so this must go through
   * fetch + a blob URL: a top-level navigation carries no Authorization header.
   */
  exportRoute: async (
    id: string,
    format: 'gpx' | 'geojson' | 'kml',
    filename?: string,
  ): Promise<void> => {
    const res = await fetch(`${BASE}/routes/${id}/export/${format}`, { headers: allHeaders() });
    if (!res.ok) throw new Error(`Export failed: ${res.status}`);

    const blob = await res.blob();
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename ?? `route-${id}.${format}`;
    // Firefox only honours a programmatic click on an anchor that is in the document,
    // and revoking the blob URL in the same tick can cancel the download that click
    // just started. Attach it, then tear both down on the next turn of the loop.
    a.style.display = 'none';
    document.body.appendChild(a);
    a.click();
    setTimeout(() => {
      a.remove();
      URL.revokeObjectURL(url);
    }, 0);
  },

  // --- Routing preview ---
  routingPreview: (waypoints: number[][], profile: string) =>
    fetchJson<{ coordinates: number[][]; distanceMeters: number; durationSeconds: number }>(
      '/routes/routing/preview',
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ waypoints, profile }),
      },
    ),

  // --- Elevation enrichment ---
  enrichElevation: (id: string) =>
    fetchJson<RouteDetail>(`/routes/${id}/elevation`, { method: 'POST' }),
};
