import type { ActivityListItem, ActivityDetail, DashboardSummary, IntegrationInfo } from '../types/activity';

const BASE = '/api';

async function fetchJson<T>(url: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${BASE}${url}`, init);
  if (!res.ok) {
    const text = await res.text();
    throw new Error(`API error ${res.status}: ${text}`);
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
      body: formData,
    });
    if (!res.ok) throw new Error(`Upload failed: ${res.status}`);
    return res.json();
  },

  deleteActivity: async (id: string) => {
    await fetch(`${BASE}/activities/${id}`, { method: 'DELETE' });
  },

  reanalyzeActivity: async (id: string) => {
    await fetch(`${BASE}/activities/${id}/reanalyze`, { method: 'POST' });
  },

  getGpxUrl: (id: string) => `${BASE}/activities/${id}/gpx`,

  // Integrations
  getIntegrations: () => fetchJson<IntegrationInfo[]>('/integrations'),

  connectIntegration: async (provider: string) => {
    const { authUrl } = await fetchJson<{ authUrl: string }>(`/integrations/${provider}/connect`, {
      method: 'POST',
    });
    window.location.href = authUrl;
  },

  disconnectIntegration: async (provider: string) => {
    await fetch(`${BASE}/integrations/${provider}`, { method: 'DELETE' });
  },
};
