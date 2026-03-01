import i18n from '../i18n';
import type {
  RouteListItem,
  RouteDetail,
  RouteCreateRequest,
  RouteUpdateRequest,
  RouteAutoSaveRequest,
} from '../types/route';

const BASE = '/api';

function langHeaders(): Record<string, string> {
  return { 'Accept-Language': i18n.language || 'en' };
}

async function fetchJson<T>(url: string, init?: RequestInit): Promise<T> {
  const headers = { ...langHeaders(), ...init?.headers };
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
      headers: { 'Content-Type': 'application/json', ...langHeaders() },
      body: JSON.stringify(data),
    });
    if (!res.ok) throw new Error(`Auto-save failed: ${res.status}`);
  },

  deleteRoute: async (id: string): Promise<void> => {
    const res = await fetch(`${BASE}/routes/${id}`, { method: 'DELETE', headers: langHeaders() });
    if (!res.ok) throw new Error(`API error ${res.status}`);
  },

  createFromActivity: (activityId: string) =>
    fetchJson<RouteDetail>(`/routes/from-activity/${activityId}`, { method: 'POST' }),

  importGpx: async (file: File): Promise<RouteDetail> => {
    const formData = new FormData();
    formData.append('file', file);
    const res = await fetch(`${BASE}/routes/import`, {
      method: 'POST',
      headers: langHeaders(),
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
  getExportUrl: (id: string, format: 'gpx' | 'geojson' | 'kml') =>
    `${BASE}/routes/${id}/export/${format}`,

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
