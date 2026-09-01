import i18n from '../i18n';
import type { ActivityListItem, ActivityDetail, DashboardSummary, IntegrationInfo, AppSettings, GlobalAppSettings, ProfilePoint, SplitsData, UserProfile, UpdateProfile } from '../types/activity';

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

let refreshInFlight: Promise<boolean> | null = null;

async function doRefresh(refreshToken: string): Promise<boolean> {
  try {
    const res = await fetch(`${BASE}/auth/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken }),
    });

    if (!res.ok) return false;

    const data = await res.json();
    if (localStorage.getItem(REFRESH_KEY) !== refreshToken)
      return false;
    localStorage.setItem(TOKEN_KEY, data.accessToken);
    localStorage.setItem(REFRESH_KEY, data.refreshToken);
    return true;
  } catch {
    return false;
  }
}

/**
 * Single-flight token refresh. The API rotates refresh tokens (each is
 * single-use), so parallel 401s must share one refresh, not race for it.
 */
export async function tryRefreshToken(): Promise<boolean> {
  if (refreshInFlight) return refreshInFlight;

  const refreshToken = localStorage.getItem(REFRESH_KEY);
  if (!refreshToken) return false;

  const p = doRefresh(refreshToken);
  refreshInFlight = p;
  void p.finally(() => {
    if (refreshInFlight === p) refreshInFlight = null;
  });
  return p;
}

/** Test-only: drops any in-flight refresh so tests start from a clean slate. */
export function __resetRefreshStateForTests(): void {
  refreshInFlight = null;
}

function forceLogout(attemptedToken: string | null): never {
  // Only clear if nobody else has already rotated us onto a fresh pair.
  if (localStorage.getItem(REFRESH_KEY) === attemptedToken) {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(REFRESH_KEY);
    window.location.href = '/login';
  }
  throw new Error('UNAUTHORIZED');
}

async function fetchJson<T>(url: string, init?: RequestInit): Promise<T> {
  const headers = { ...allHeaders(), ...init?.headers };
  let res = await fetch(`${BASE}${url}`, { cache: 'no-cache', ...init, headers });

  // 401 → try refresh token, then retry once
  if (res.status === 401) {
    const attempted = localStorage.getItem(REFRESH_KEY);
    const refreshed = await tryRefreshToken();
    if (refreshed) {
      const retryHeaders = { ...allHeaders(), ...init?.headers };
      res = await fetch(`${BASE}${url}`, { cache: 'no-cache', ...init, headers: retryHeaders });
    } else {
      forceLogout(attempted);
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
    const attempted = localStorage.getItem(REFRESH_KEY);
    const refreshed = await tryRefreshToken();
    if (refreshed) {
      const retryHeaders = { ...allHeaders(), ...init?.headers };
      res = await fetch(`${BASE}${url}`, { ...init, headers: retryHeaders });
    } else {
      forceLogout(attempted);
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

  fixAnomalies: async (id: string) => {
    await fetchWithAuth(`/activities/${id}/fix-anomalies`, { method: 'POST' });
  },

  updateActivity: (id: string, data: {
    activityType?: string;
    name?: string;
    description?: string;
    perceivedExertion?: number | null;
    tags?: string[];
    sessionType?: string;
  }) =>
    fetchJson<{ id: string; activityType: string; name: string }>(`/activities/${id}`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data),
    }),

  getTags: () => fetchJson<string[]>('/activities/tags'),

  getProfile: (id: string) => fetchJson<ProfilePoint[]>(`/activities/${id}/profile`),

  getTrack: (id: string) => fetchJson<{ type: string; coordinates: number[][] }>(`/activities/${id}/track`),

  getSplits: (id: string) => fetchJson<SplitsData>(`/activities/${id}/splits`),

  downloadGpx: async (id: string, filename: string): Promise<void> => {
    const res = await fetchWithAuth(`/activities/${id}/gpx`);
    if (!res.ok) throw new Error('GPX_NOT_FOUND');
    const blob = await res.blob();
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename.endsWith('.gpx') ? filename : `${filename}.gpx`;
    a.click();
    URL.revokeObjectURL(url);
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

  // Settings (user-scoped: analysis preferences)
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

  // Global settings (admin only: AI provider + integration credentials)
  getGlobalSettings: () => fetchJson<GlobalAppSettings>('/settings/global'),

  updateGlobalSettings: async (settings: GlobalAppSettings): Promise<void> => {
    const res = await fetchWithAuth('/settings/global', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(settings),
    });
    if (!res.ok) {
      const text = await res.text();
      // A rejected save carries { code, message } and the message is the whole point
      // of the rejection (it names the config key to set and the callback URL to
      // re-register). Surface it instead of dumping the raw JSON envelope.
      let detail = text;
      try {
        const parsed = JSON.parse(text);
        detail = parsed?.message ?? parsed?.code ?? text;
      } catch {
        /* not JSON — fall back to the raw body */
      }
      throw new Error(detail || `API error ${res.status}`);
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
