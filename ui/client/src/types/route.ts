export interface RouteListItem {
  id: string;
  name: string;
  activityType: string;
  routeCategory: string;
  status: string;
  distanceKm: number;
  elevationGainM: number;
  estimatedTimeSeconds: number;
  tags: string | null;
  routingProfile: string;
  createdAt: string;
  updatedAt: string;
}

export interface RouteDetail {
  id: string;
  name: string;
  description: string | null;
  activityType: string;
  routeCategory: string;
  status: string;
  distanceKm: number;
  elevationGainM: number;
  elevationLossM: number;
  maxElevationM: number;
  minElevationM: number;
  estimatedTimeSeconds: number;
  tags: string | null;
  routingProfile: string;
  sourceActivityId: string | null;
  sourceFileName: string | null;
  createdAt: string;
  updatedAt: string;
  points: number[][] | null;
  waypoints: RouteWaypoint[] | null;
  pois: RoutePoi[] | null;
  profile: unknown | null;
}

export interface RouteWaypoint {
  id: string;
  lat: number;
  lon: number;
  order: number;
}

export interface RoutePoi {
  id: string;
  type: PoiType;
  name: string;
  lat: number;
  lon: number;
  notes?: string;
}

export type PoiType = 'water' | 'parking' | 'refuge' | 'summit' | 'viewpoint' | 'danger' | 'food' | 'camping' | 'custom';

export type RouteCategory = 'loop' | 'out-and-back' | 'traverse' | 'point-to-point';

export type RouteStatus = 'draft' | 'published';

export type RoutingProfile = 'manual' | 'hiking' | 'trail' | 'cycling' | 'road';

export interface RouteCreateRequest {
  name?: string;
  activityType?: string;
  sourceActivityId?: string;
}

export interface RouteUpdateRequest {
  name: string;
  description?: string;
  activityType: string;
  routeCategory: string;
  tags?: string;
  routingProfile: string;
  status: string;
  points?: number[][];
  waypoints?: RouteWaypoint[];
  pois?: RoutePoi[];
}

export interface RouteAutoSaveRequest {
  points?: number[][];
  waypoints?: RouteWaypoint[];
  pois?: RoutePoi[];
}
