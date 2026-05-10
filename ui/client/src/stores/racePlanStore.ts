import { create } from 'zustand';

export type RacePlanTab = 'timeline' | 'nutrition' | 'equipment' | 'notes';

export interface RacePlanUIState {
  // Tab navigation
  activeTab: RacePlanTab;

  // Checkpoint editing
  editingCheckpointId: string | null;
  showCheckpointEditor: boolean;

  // Cross-component hover sync (map ↔ elevation chart ↔ timeline)
  hoveredDistanceKm: number | null;

  // New checkpoint placement (via click on elevation profile)
  newCheckpointDistanceKm: number | null;

  // Share modal
  showShareModal: boolean;

  // Nutrition item editing
  editingNutritionPlanId: string | null;
  editingNutritionItemId: string | null;
}

export interface RacePlanUIActions {
  setActiveTab: (tab: RacePlanTab) => void;

  openCheckpointEditor: (checkpointId: string | null, distanceKm?: number) => void;
  closeCheckpointEditor: () => void;

  setHoveredDistanceKm: (km: number | null) => void;

  openShareModal: () => void;
  closeShareModal: () => void;

  openNutritionEditor: (planId: string, itemId?: string | null) => void;
  closeNutritionEditor: () => void;

  reset: () => void;
}

const initialState: RacePlanUIState = {
  activeTab: 'timeline',
  editingCheckpointId: null,
  showCheckpointEditor: false,
  hoveredDistanceKm: null,
  newCheckpointDistanceKm: null,
  showShareModal: false,
  editingNutritionPlanId: null,
  editingNutritionItemId: null,
};

export const useRacePlanStore = create<RacePlanUIState & RacePlanUIActions>()((set) => ({
  ...initialState,

  setActiveTab: (tab) => set({ activeTab: tab }),

  openCheckpointEditor: (checkpointId, distanceKm) =>
    set({
      editingCheckpointId: checkpointId,
      showCheckpointEditor: true,
      newCheckpointDistanceKm: distanceKm ?? null,
    }),

  closeCheckpointEditor: () =>
    set({
      showCheckpointEditor: false,
      editingCheckpointId: null,
      newCheckpointDistanceKm: null,
    }),

  setHoveredDistanceKm: (km) => set({ hoveredDistanceKm: km }),

  openShareModal: () => set({ showShareModal: true }),
  closeShareModal: () => set({ showShareModal: false }),

  openNutritionEditor: (planId, itemId) =>
    set({ editingNutritionPlanId: planId, editingNutritionItemId: itemId ?? null }),

  closeNutritionEditor: () =>
    set({ editingNutritionPlanId: null, editingNutritionItemId: null }),

  reset: () => set(initialState),
}));
