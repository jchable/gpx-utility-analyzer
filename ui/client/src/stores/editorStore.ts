import { create } from 'zustand';
import { temporal } from 'zundo';
import type { RouteWaypoint, RoutePoi, PoiType, RoutingProfile } from '../types/route';

export type EditorMode = 'select' | 'addPoint' | 'freehand' | 'split' | 'crop' | 'addPoi';

export interface EditorState {
  // Mode & routing
  mode: EditorMode;
  routingProfile: RoutingProfile;

  // Route data
  waypoints: RouteWaypoint[];
  routeCoordinates: number[][];
  pois: RoutePoi[];

  // Selection & interaction
  selectedWaypointId: string | null;
  selectedPoiId: string | null;
  hoveredPointIndex: number | null;
  isRouteLoading: boolean;

  // Dirty tracking
  isDirty: boolean;
  lastAutoSave: Date | null;

  // Route metadata (kept here for editor convenience)
  routeId: string | null;
  routeName: string;
  routeDescription: string;
  activityType: string;
  routeCategory: string;
  tags: string;
}

export interface EditorActions {
  // Mode
  setMode: (mode: EditorMode) => void;
  setRoutingProfile: (profile: RoutingProfile) => void;

  // Waypoints
  addWaypoint: (lat: number, lon: number, order?: number) => void;
  moveWaypoint: (id: string, lat: number, lon: number) => void;
  deleteWaypoint: (id: string) => void;
  insertWaypoint: (lat: number, lon: number, afterOrder: number) => void;

  // Route coordinates (from routing or manual)
  setRouteCoordinates: (coords: number[][]) => void;
  setIsRouteLoading: (loading: boolean) => void;

  // POIs
  addPoi: (type: PoiType, lat: number, lon: number, name?: string) => void;
  movePoi: (id: string, lat: number, lon: number) => void;
  deletePoi: (id: string) => void;
  updatePoi: (id: string, updates: Partial<RoutePoi>) => void;

  // Selection
  selectWaypoint: (id: string | null) => void;
  selectPoi: (id: string | null) => void;
  setHoveredPointIndex: (index: number | null) => void;

  // Track operations
  reverseRoute: () => void;
  splitRouteAt: (index: number) => number[][]; // returns the second part
  cropRoute: (startIndex: number, endIndex: number) => void;
  mergeCoordinates: (coords: number[][]) => void;

  // Metadata
  setRouteName: (name: string) => void;
  setRouteDescription: (desc: string) => void;
  setActivityType: (type: string) => void;
  setRouteCategory: (cat: string) => void;
  setTags: (tags: string) => void;

  // Lifecycle
  setRouteId: (id: string | null) => void;
  markSaved: () => void;
  loadRoute: (data: {
    id: string;
    name: string;
    description?: string;
    activityType: string;
    routeCategory: string;
    tags?: string;
    routingProfile: string;
    points?: number[][] | null;
    waypoints?: RouteWaypoint[] | null;
    pois?: RoutePoi[] | null;
  }) => void;
  reset: () => void;
}

function generateId(): string {
  return crypto.randomUUID();
}

const initialState: EditorState = {
  mode: 'select',
  routingProfile: 'manual',
  waypoints: [],
  routeCoordinates: [],
  pois: [],
  selectedWaypointId: null,
  selectedPoiId: null,
  hoveredPointIndex: null,
  isRouteLoading: false,
  isDirty: false,
  lastAutoSave: null,
  routeId: null,
  routeName: '',
  routeDescription: '',
  activityType: 'trail',
  routeCategory: '',
  tags: '',
};

export const useEditorStore = create<EditorState & EditorActions>()(
  temporal(
    (set, get) => ({
      ...initialState,

      // --- Mode ---
      setMode: (mode) => set({ mode, selectedWaypointId: null, selectedPoiId: null }),
      setRoutingProfile: (routingProfile) => set({ routingProfile, isDirty: true }),

      // --- Waypoints ---
      addWaypoint: (lat, lon, order) => {
        const wp: RouteWaypoint = {
          id: generateId(),
          lat,
          lon,
          order: order ?? get().waypoints.length,
        };
        set((s) => ({
          waypoints: [...s.waypoints, wp].sort((a, b) => a.order - b.order),
          isDirty: true,
        }));
      },

      moveWaypoint: (id, lat, lon) =>
        set((s) => ({
          waypoints: s.waypoints.map((wp) =>
            wp.id === id ? { ...wp, lat, lon } : wp,
          ),
          isDirty: true,
        })),

      deleteWaypoint: (id) =>
        set((s) => ({
          waypoints: s.waypoints
            .filter((wp) => wp.id !== id)
            .map((wp, i) => ({ ...wp, order: i })),
          selectedWaypointId: s.selectedWaypointId === id ? null : s.selectedWaypointId,
          isDirty: true,
        })),

      insertWaypoint: (lat, lon, afterOrder) => {
        const wp: RouteWaypoint = {
          id: generateId(),
          lat,
          lon,
          order: afterOrder + 1,
        };
        set((s) => ({
          waypoints: [...s.waypoints.map((w) =>
            w.order > afterOrder ? { ...w, order: w.order + 1 } : w,
          ), wp].sort((a, b) => a.order - b.order),
          isDirty: true,
        }));
      },

      // --- Route coordinates ---
      setRouteCoordinates: (coords) => set({ routeCoordinates: coords, isDirty: true }),
      setIsRouteLoading: (loading) => set({ isRouteLoading: loading }),

      // --- POIs ---
      addPoi: (type, lat, lon, name) => {
        const poi: RoutePoi = {
          id: generateId(),
          type,
          name: name ?? type,
          lat,
          lon,
        };
        set((s) => ({ pois: [...s.pois, poi], isDirty: true }));
      },

      movePoi: (id, lat, lon) =>
        set((s) => ({
          pois: s.pois.map((p) => (p.id === id ? { ...p, lat, lon } : p)),
          isDirty: true,
        })),

      deletePoi: (id) =>
        set((s) => ({
          pois: s.pois.filter((p) => p.id !== id),
          selectedPoiId: s.selectedPoiId === id ? null : s.selectedPoiId,
          isDirty: true,
        })),

      updatePoi: (id, updates) =>
        set((s) => ({
          pois: s.pois.map((p) => (p.id === id ? { ...p, ...updates } : p)),
          isDirty: true,
        })),

      // --- Selection ---
      selectWaypoint: (id) => set({ selectedWaypointId: id, selectedPoiId: null }),
      selectPoi: (id) => set({ selectedPoiId: id, selectedWaypointId: null }),
      setHoveredPointIndex: (index) => set({ hoveredPointIndex: index }),

      // --- Track operations ---
      reverseRoute: () =>
        set((s) => {
          const reversed = [...s.routeCoordinates].reverse();
          const reversedWp = s.waypoints
            .map((wp, _i, arr) => ({ ...wp, order: arr.length - 1 - wp.order }))
            .sort((a, b) => a.order - b.order);
          return { routeCoordinates: reversed, waypoints: reversedWp, isDirty: true };
        }),

      splitRouteAt: (index) => {
        const coords = get().routeCoordinates;
        if (index <= 0 || index >= coords.length - 1) return [];
        const first = coords.slice(0, index + 1);
        const second = coords.slice(index);
        set({ routeCoordinates: first, isDirty: true });
        return second;
      },

      cropRoute: (startIndex, endIndex) =>
        set((s) => {
          const cropped = s.routeCoordinates.slice(startIndex, endIndex + 1);
          return { routeCoordinates: cropped, isDirty: true };
        }),

      mergeCoordinates: (coords) =>
        set((s) => ({
          routeCoordinates: [...s.routeCoordinates, ...coords],
          isDirty: true,
        })),

      // --- Metadata ---
      setRouteName: (name) => set({ routeName: name, isDirty: true }),
      setRouteDescription: (desc) => set({ routeDescription: desc, isDirty: true }),
      setActivityType: (type) => set({ activityType: type, isDirty: true }),
      setRouteCategory: (cat) => set({ routeCategory: cat, isDirty: true }),
      setTags: (tags) => set({ tags, isDirty: true }),

      // --- Lifecycle ---
      setRouteId: (id) => set({ routeId: id }),
      markSaved: () => set({ isDirty: false, lastAutoSave: new Date() }),

      loadRoute: (data) =>
        set({
          routeId: data.id,
          routeName: data.name,
          routeDescription: data.description ?? '',
          activityType: data.activityType,
          routeCategory: data.routeCategory,
          tags: data.tags ?? '',
          routingProfile: (data.routingProfile as RoutingProfile) || 'manual',
          routeCoordinates: data.points ?? [],
          waypoints: data.waypoints ?? [],
          pois: data.pois ?? [],
          isDirty: false,
          lastAutoSave: null,
          selectedWaypointId: null,
          selectedPoiId: null,
          hoveredPointIndex: null,
          mode: 'select',
        }),

      reset: () => set(initialState),
    }),
    {
      limit: 50,
      equality: (past, current) => {
        // Only track changes to route data, not UI state
        return (
          past.waypoints === current.waypoints &&
          past.routeCoordinates === current.routeCoordinates &&
          past.pois === current.pois &&
          past.routeName === current.routeName &&
          past.routeDescription === current.routeDescription &&
          past.activityType === current.activityType &&
          past.routeCategory === current.routeCategory &&
          past.tags === current.tags &&
          past.routingProfile === current.routingProfile
        );
      },
    },
  ),
);
