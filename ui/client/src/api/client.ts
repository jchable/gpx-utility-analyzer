import i18n from '../i18n';
import type { ActivityListItem, ActivityDetail, DashboardSummary, IntegrationInfo, AppSettings, ProfilePoint, SplitsData, PredictResult } from '../types/activity';

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

export const api = {
  // Dashboard
  getDashboardSummary: () => fetchJson<DashboardSummary>('/dashboard/summary'),

  // Activities
  getActivities: (page = 1, pageSize = 20, type?: string) => {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (type) params.set('type', type);
    return fetchJson<ActivityListItem[]>(`/activities?${params}`);
  },

  getActivity: (id: string) => fetchJson<ActivityDetail>(`/activities/${id}`),

  uploadGpx: async (file: File, activityType?: string): Promise<ActivityDetail> => {
    const formData = new FormData();
    formData.append('file', file);
    if (activityType) formData.append('activityType', activityType);
    const res = await fetch(`${BASE}/activities/upload`, {
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
      throw new Error(`Upload failed: ${res.status}`);
    }
    return res.json();
  },

  deleteActivity: async (id: string) => {
    const res = await fetch(`${BASE}/activities/${id}`, { method: 'DELETE', headers: langHeaders() });
    if (!res.ok) throw new Error(`API error ${res.status}`);
  },

  reanalyzeActivity: async (id: string) => {
    await fetch(`${BASE}/activities/${id}/reanalyze`, { method: 'POST', headers: langHeaders() });
  },

  getProfile: (id: string) => fetchJson<ProfilePoint[]>(`/activities/${id}/profile`),

  getTrack: (id: string) => fetchJson<{ type: string; coordinates: number[][] }>(`/activities/${id}/track`),

  getSplits: (id: string) => fetchJson<SplitsData>(`/activities/${id}/splits`),

  getGpxUrl: (id: string) => `${BASE}/activities/${id}/gpx`,

  predictRoute: async (file: File): Promise<PredictResult> => {
    const formData = new FormData();
    formData.append('file', file);
    const res = await fetch(`${BASE}/activities/predict`, {
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
      throw new Error(`Predict failed: ${res.status}`);
    }
    return res.json();
  },

  // Integrations
  getIntegrations: () => fetchJson<IntegrationInfo[]>('/integrations'),

  connectIntegration: async (provider: string) => {
    const { authUrl } = await fetchJson<{ authUrl: string }>(`/integrations/${provider}/connect`, {
      method: 'POST',
    });
    window.location.href = authUrl;
  },

  disconnectIntegration: async (provider: string) => {
    await fetch(`${BASE}/integrations/${provider}`, { method: 'DELETE', headers: langHeaders() });
  },

  // Settings
  getSettings: () => fetchJson<AppSettings>('/settings'),

  updateSettings: async (settings: AppSettings): Promise<void> => {
    const res = await fetch(`${BASE}/settings`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json', ...langHeaders() },
      body: JSON.stringify(settings),
    });
    if (!res.ok) {
      const text = await res.text();
      throw new Error(`API error ${res.status}: ${text}`);
    }
  },

  getProviders: () => fetchJson<string[]>('/settings/providers'),
};
