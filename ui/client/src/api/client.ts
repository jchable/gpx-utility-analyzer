import i18n from '../i18n';
import type { ActivityListItem, ActivityDetail, DashboardSummary, IntegrationInfo, AppSettings, ProfilePoint, SplitsData, PredictResult, UserProfile, UpdateProfile } from '../types/activity';

const BASE = '/api';

const TOKEN_KEY = 'gpx_access_token';
const REFRESH_KEY = 'gpx_refresh_token';

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

async function tryRefreshToken(): Promise<boolean> {
  const refreshToken = localStorage.getItem(REFRESH_KEY);
  if (!refreshToken) return false;

  try {
    const res = await fetch(`${BASE}/auth/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken }),
    });

    if (!res.ok) return false;

    const data = await res.json();
    localStorage.setItem(TOKEN_KEY, data.accessToken);
    localStorage.setItem(REFRESH_KEY, data.refreshToken);
    return true;
  } catch {
    return false;
  }
}

async function fetchJson<T>(url: string, init?: RequestInit): Promise<T> {
  const headers = { ...allHeaders(), ...init?.headers };
  let res = await fetch(`${BASE}${url}`, { cache: 'no-cache', ...init, headers });

  // 401 → try refresh token, then retry once
  if (res.status === 401) {
    const refreshed = await tryRefreshToken();
    if (refreshed) {
      const retryHeaders = { ...allHeaders(), ...init?.headers };
      res = await fetch(`${BASE}${url}`, { cache: 'no-cache', ...init, headers: retryHeaders });
    } else {
      // Clear tokens and redirect to login
      localStorage.removeItem(TOKEN_KEY);
      localStorage.removeItem(REFRESH_KEY);
      window.location.href = '/login';
      throw new Error('UNAUTHORIZED');
    }
  }

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

async function fetchWithAuth(url: string, init?: RequestInit): Promise<Response> {
  const headers = { ...allHeaders(), ...init?.headers };
  let res = await fetch(`${BASE}${url}`, { ...init, headers });

  if (res.status === 401) {
    const refreshed = await tryRefreshToken();
    if (refreshed) {
      const retryHeaders = { ...allHeaders(), ...init?.headers };
      res = await fetch(`${BASE}${url}`, { ...init, headers: retryHeaders });
    } else {
      localStorage.removeItem(TOKEN_KEY);
      localStorage.removeItem(REFRESH_KEY);
      window.location.href = '/login';
      throw new Error('UNAUTHORIZED');
    }
  }

  return res;
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
    const res = await fetchWithAuth('/activities/upload', {
      method: 'POST',
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
    const res = await fetchWithAuth(`/activities/${id}`, { method: 'DELETE' });
    if (!res.ok) throw new Error(`API error ${res.status}`);
  },

  reanalyzeActivity: async (id: string) => {
    await fetchWithAuth(`/activities/${id}/reanalyze`, { method: 'POST' });
  },

  updateActivity: (id: string, data: { activityType?: string; name?: string }) =>
    fetchJson<{ id: string; activityType: string; name: string }>(`/activities/${id}`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data),
    }),

  getProfile: (id: string) => fetchJson<ProfilePoint[]>(`/activities/${id}/profile`),

  getTrack: (id: string) => fetchJson<{ type: string; coordinates: number[][] }>(`/activities/${id}/track`),

  getSplits: (id: string) => fetchJson<SplitsData>(`/activities/${id}/splits`),

  getGpxUrl: (id: string) => `${BASE}/activities/${id}/gpx`,

  predictRoute: async (file: File): Promise<PredictResult> => {
    const formData = new FormData();
    formData.append('file', file);
    const res = await fetchWithAuth('/activities/predict', {
      method: 'POST',
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
    await fetchWithAuth(`/integrations/${provider}`, { method: 'DELETE' });
  },

  // Settings
  getSettings: async (): Promise<AppSettings> => {
    const [settings, profile] = await Promise.all([
      fetchJson<AppSettings>('/settings'),
      fetchJson<UserProfile>('/profile').catch(() => null),
    ]);
    if (profile) {
      settings.athlete = {
        maxHeartRate: profile.maxHeartRate,
        restingHeartRate: profile.restingHeartRate,
        ftp: profile.ftp,
        vo2Max: profile.vo2Max,
        age: profile.age,
      };
    }
    return settings;
  },

  updateSettings: async (settings: AppSettings): Promise<void> => {
    const res = await fetchWithAuth('/settings', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(settings),
    });
    if (!res.ok) {
      const text = await res.text();
      throw new Error(`API error ${res.status}: ${text}`);
    }
  },

  getProviders: () => fetchJson<string[]>('/settings/providers'),

  // Profile
  getUserProfile: () => fetchJson<UserProfile>('/profile'),

  updateUserProfile: (data: UpdateProfile) =>
    fetchJson<UserProfile>('/profile', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data),
    }),

  changePassword: async (currentPassword: string, newPassword: string): Promise<void> => {
    const res = await fetchWithAuth('/profile/change-password', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ currentPassword, newPassword }),
    });
    if (!res.ok) {
      let code = '';
      try {
        const json = await res.json();
        code = json.code || '';
      } catch { /* not JSON */ }
      if (code) throw new Error(code);
      throw new Error(`API error ${res.status}`);
    }
  },
};
