import i18n from '../i18n';
import type {
  RacePlanListItem,
  RacePlanDetail,
  RacePlanShared,
  RacePlanUpdateRequest,
  RacePlanCheckpointCreateRequest,
  RacePlanCheckpointUpdateRequest,
  RacePlanNutritionItemCreateRequest,
  RacePlanComparison,
  NutritionProduct,
  NutritionProductCreateRequest,
  NutritionProductUpdateRequest,
} from '../types/race-plan';

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

async function fetchVoid(url: string, init?: RequestInit): Promise<void> {
  const headers = { ...allHeaders(), ...init?.headers };
  const res = await fetch(`${BASE}${url}`, { cache: 'no-cache', ...init, headers });
  if (!res.ok) throw new Error(`API error ${res.status}`);
}

// ─────────────────────────────────────────────
// Race Plans
// ─────────────────────────────────────────────

export const racePlansApi = {
  // List
  getPlans: (page = 1, pageSize = 20, type?: string, status?: string) => {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (type) params.set('type', type);
    if (status) params.set('status', status);
    return fetchJson<RacePlanListItem[]>(`/race-plans?${params}`);
  },

  // Get detail
  getPlan: (id: string) => fetchJson<RacePlanDetail>(`/race-plans/${id}`),

  // Get shared (public, no auth needed)
  getShared: (token: string) => fetchJson<RacePlanShared>(`/race-plans/share/${token}`),

  // Create from Route
  createFromRoute: (routeId: string) =>
    fetchJson<RacePlanDetail>(`/race-plans/from-route/${routeId}`, { method: 'POST' }),

  // Import GPX
  importGpx: (file: File) => {
    const formData = new FormData();
    formData.append('file', file);
    return fetchJson<RacePlanDetail>('/race-plans/import', {
      method: 'POST',
      headers: { ...langHeaders(), ...authHeaders() },
      body: formData,
    });
  },

  // Update
  updatePlan: (id: string, data: RacePlanUpdateRequest) =>
    fetchJson<RacePlanDetail>(`/race-plans/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data),
    }),

  // Delete
  deletePlan: (id: string) => fetchVoid(`/race-plans/${id}`, { method: 'DELETE' }),

  // Checkpoints
  addCheckpoint: (planId: string, data: RacePlanCheckpointCreateRequest) =>
    fetchJson<RacePlanDetail>(`/race-plans/${planId}/checkpoints`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data),
    }),

  updateCheckpoint: (planId: string, checkpointId: string, data: RacePlanCheckpointUpdateRequest) =>
    fetchJson<RacePlanDetail>(`/race-plans/${planId}/checkpoints/${checkpointId}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data),
    }),

  deleteCheckpoint: (planId: string, checkpointId: string) =>
    fetchVoid(`/race-plans/${planId}/checkpoints/${checkpointId}`, { method: 'DELETE' }),

  // Recalcul des temps
  computeTimes: (planId: string) =>
    fetchJson<RacePlanDetail>(`/race-plans/${planId}/compute-times`, { method: 'POST' }),

  // Nutrition
  addNutritionItem: (planId: string, data: RacePlanNutritionItemCreateRequest) =>
    fetchJson<RacePlanDetail>(`/race-plans/${planId}/nutrition`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data),
    }),

  deleteNutritionItem: (planId: string, itemId: string) =>
    fetchVoid(`/race-plans/${planId}/nutrition/${itemId}`, { method: 'DELETE' }),

  // Partage crew
  enableShare: (planId: string) =>
    fetchJson<{ token: string; shareUrl: string }>(`/race-plans/${planId}/share`, { method: 'POST' }),

  disableShare: (planId: string) =>
    fetchVoid(`/race-plans/${planId}/share`, { method: 'DELETE' }),

  // Post-course
  linkActivity: (planId: string, activityId: string) =>
    fetchVoid(`/race-plans/${planId}/link-activity/${activityId}`, { method: 'POST' }),

  getComparison: (planId: string) =>
    fetchJson<RacePlanComparison>(`/race-plans/${planId}/comparison`),
};

// ─────────────────────────────────────────────
// Nutrition Products
// ─────────────────────────────────────────────

export const nutritionProductsApi = {
  getProducts: (type?: string) => {
    const params = new URLSearchParams();
    if (type) params.set('type', type);
    return fetchJson<NutritionProduct[]>(`/nutrition-products?${params}`);
  },

  createProduct: (data: NutritionProductCreateRequest) =>
    fetchJson<NutritionProduct>('/nutrition-products', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data),
    }),

  updateProduct: (id: string, data: NutritionProductUpdateRequest) =>
    fetchJson<NutritionProduct>(`/nutrition-products/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data),
    }),

  deleteProduct: (id: string) =>
    fetchVoid(`/nutrition-products/${id}`, { method: 'DELETE' }),

  importDefaults: () =>
    fetchJson<{ imported: number }>('/nutrition-products/import-defaults', { method: 'POST' }),
};
