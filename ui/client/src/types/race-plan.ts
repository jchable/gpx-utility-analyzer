// ─────────────────────────────────────────────
// Race Plan Types
// ─────────────────────────────────────────────

export interface RacePlanListItem {
  id: string;
  name: string;
  description: string | null;
  activityType: string;
  status: RacePlanStatus;
  distanceKm: number;
  elevationGainM: number;
  elevationLossM: number;
  raceDate: string | null;       // ISO 8601 date
  startTime: string | null;      // "HH:mm"
  targetTimeSeconds: number | null;
  targetTimeBSeconds: number | null;
  targetTimeCSeconds: number | null;
  performanceCoefficient: number;
  checkpointCount: number;
  isPublic: boolean;
  linkedActivityId: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface RacePlanDetail {
  id: string;
  name: string;
  description: string | null;
  activityType: string;
  status: RacePlanStatus;
  language: string;
  routeId: string | null;

  // Stats trace
  distanceKm: number;
  elevationGainM: number;
  elevationLossM: number;
  maxElevationM: number;
  minElevationM: number;

  // Race details
  raceDate: string | null;
  startTime: string | null;      // "HH:mm"
  startLatitude: number | null;
  startLongitude: number | null;

  // Objectifs
  targetTimeSeconds: number | null;
  targetTimeBSeconds: number | null;
  targetTimeCSeconds: number | null;
  performanceCoefficient: number;
  sweatRateMLPerHour: number | null;

  // Équipement
  equipment: RacePlanEquipmentItem[] | null;

  // Partage
  isPublic: boolean;
  shareToken: string | null;

  linkedActivityId: string | null;

  checkpoints: RacePlanCheckpoint[];
  nutritionItems: RacePlanNutritionItem[];
  profile: RacePlanProfilePoint[] | null;
  points: number[][] | null;     // [lon, lat, ele][]

  createdAt: string;
  updatedAt: string;
}

// Vue partagée crew (sans données privées)
export interface RacePlanShared {
  id: string;
  name: string;
  description: string | null;
  activityType: string;
  distanceKm: number;
  elevationGainM: number;
  elevationLossM: number;
  raceDate: string | null;
  startTime: string | null;
  targetTimeSeconds: number | null;
  targetTimeBSeconds: number | null;
  targetTimeCSeconds: number | null;
  checkpoints: RacePlanCheckpointShared[];
  profile: RacePlanProfilePoint[] | null;
  points: number[][] | null;
}

// ─────────────────────────────────────────────
// Checkpoint
// ─────────────────────────────────────────────

export interface RacePlanCheckpoint {
  id: string;
  order: number;
  name: string;
  type: CheckpointType;
  distanceKm: number;
  elevationM: number | null;
  latitude: number | null;
  longitude: number | null;
  cutoffTimeSeconds: number | null;   // Cutoff officiel (secondes depuis départ)
  targetArrivalSeconds: number | null; // Arrivée prévue (calculée auto)
  plannedPauseSeconds: number | null;
  isCrewAccessible: boolean;
  crewNotes: string | null;
  hasDropBag: boolean;
  dropBagContents: DropBagItem[] | null;
  equipmentTake: string[] | null;
  equipmentLeave: string[] | null;
  notes: string | null;
}

// Version simplifiée pour la vue crew
export interface RacePlanCheckpointShared {
  id: string;
  order: number;
  name: string;
  type: CheckpointType;
  distanceKm: number;
  elevationM: number | null;
  latitude: number | null;
  longitude: number | null;
  cutoffTimeSeconds: number | null;
  targetArrivalSeconds: number | null;
  plannedPauseSeconds: number | null;
  isCrewAccessible: boolean;
  crewNotes: string | null;
}

export type CheckpointType = 'start' | 'checkpoint' | 'aid_station' | 'crew_only' | 'finish';

// ─────────────────────────────────────────────
// Équipement
// ─────────────────────────────────────────────

export interface RacePlanEquipmentItem {
  name: string;
  category: EquipmentCategory;
  isMandatory: boolean;
  notes?: string;
}

export type EquipmentCategory =
  | 'clothing'
  | 'footwear'
  | 'navigation'
  | 'nutrition'
  | 'safety'
  | 'lighting'
  | 'other';

// ─────────────────────────────────────────────
// Drop bag
// ─────────────────────────────────────────────

export interface DropBagItem {
  item: string;
  qty: number;
}

// ─────────────────────────────────────────────
// Profil d'élévation (500 points)
// ─────────────────────────────────────────────

export interface RacePlanProfilePoint {
  distance: number;      // km cumulé
  elevation: number;     // mètres
  grade: number;         // % (positif = montée)
  toblerSpeed: number;   // km/h théorique Tobler
}

// ─────────────────────────────────────────────
// Nutrition
// ─────────────────────────────────────────────

export interface RacePlanNutritionItem {
  id: string;
  atCheckpointId: string | null;
  fromCheckpointId: string | null;
  toCheckpointId: string | null;
  productId: string | null;
  productName: string;
  caloriesKcal: number | null;
  carbsG: number | null;
  sodiumMg: number | null;
  quantity: number;
  unit: 'unit' | 'ml' | 'g';
  timeOffsetSeconds: number | null;
  notes: string | null;
}

export interface NutritionProduct {
  id: string;
  name: string;
  brand: string | null;
  type: NutritionProductType;
  caloriesKcal: number;
  carbsG: number;
  proteinsG: number | null;
  fatsG: number | null;
  sodiumMg: number | null;
  caffeineG: number | null;
  weightG: number | null;
  volumeML: number | null;
  notes: string | null;
  createdAt: string;
  updatedAt: string;
}

export type NutritionProductType =
  | 'gel'
  | 'bar'
  | 'drink'
  | 'real_food'
  | 'electrolyte'
  | 'supplement';

// ─────────────────────────────────────────────
// Comparaison post-course
// ─────────────────────────────────────────────

export interface RacePlanComparison {
  racePlanId: string;
  activityId: string;
  checkpoints: RacePlanCheckpointComparison[];
}

export interface RacePlanCheckpointComparison {
  checkpointId: string;
  checkpointName: string;
  distanceKm: number;
  plannedSeconds: number | null;
  actualSeconds: number | null;
  deltaSeconds: number | null;   // positif = en retard, négatif = en avance
}

// ─────────────────────────────────────────────
// Types utilitaires
// ─────────────────────────────────────────────

export type RacePlanStatus = 'draft' | 'ready' | 'archived';

// ─────────────────────────────────────────────
// Request types (Create / Update)
// ─────────────────────────────────────────────

// Empty request body — routeId is passed in the URL
export type RacePlanCreateFromRouteRequest = Record<string, never>;

export interface RacePlanUpdateRequest {
  name: string;
  description?: string;
  activityType: string;
  status: string;
  raceDate?: string | null;
  startTime?: string | null;      // "HH:mm"
  startLatitude?: number | null;
  startLongitude?: number | null;
  targetTimeSeconds?: number | null;
  targetTimeBSeconds?: number | null;
  targetTimeCSeconds?: number | null;
  performanceCoefficient: number;
  sweatRateMLPerHour?: number | null;
  equipment?: RacePlanEquipmentItem[] | null;
}

export interface RacePlanCheckpointCreateRequest {
  name: string;
  type: CheckpointType;
  distanceKm: number;
  cutoffTimeSeconds?: number | null;
  plannedPauseSeconds?: number | null;
  isCrewAccessible?: boolean;
  crewNotes?: string | null;
  hasDropBag?: boolean;
  dropBagContents?: DropBagItem[] | null;
  equipmentTake?: string[] | null;
  equipmentLeave?: string[] | null;
  notes?: string | null;
}

// Same structure as the create request
export type RacePlanCheckpointUpdateRequest = RacePlanCheckpointCreateRequest;

export interface RacePlanNutritionItemCreateRequest {
  atCheckpointId?: string | null;
  fromCheckpointId?: string | null;
  toCheckpointId?: string | null;
  productId?: string | null;
  productName?: string;
  quantity: number;
  unit: 'unit' | 'ml' | 'g';
  timeOffsetSeconds?: number | null;
  notes?: string | null;
}

export interface NutritionProductCreateRequest {
  name: string;
  brand?: string | null;
  type: NutritionProductType;
  caloriesKcal: number;
  carbsG: number;
  proteinsG?: number | null;
  fatsG?: number | null;
  sodiumMg?: number | null;
  caffeineG?: number | null;
  weightG?: number | null;
  volumeML?: number | null;
  notes?: string | null;
}

export type NutritionProductUpdateRequest = NutritionProductCreateRequest;
