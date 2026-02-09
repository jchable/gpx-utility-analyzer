export interface ActivityListItem {
  id: string;
  name: string;
  activityType: string;
  startTime: string;
  distanceKm: number;
  elevationGainM: number;
  movingTimeSeconds: number;
  source: string;
  status: string;
}

export interface ActivityDetail {
  id: string;
  name: string;
  activityType: string;
  startTime: string;
  endTime: string;
  distanceKm: number;
  elevationGainM: number;
  elevationLossM: number;
  movingTimeSeconds: number;
  source: string;
  status: string;
  errorMessage?: string;
  stats?: GpxStats;
  aiReport?: TrackReport;
  createdAt: string;
  updatedAt: string;
}

export interface GpxStats {
  filename: string;
  total_distance_m: number;
  total_distance_3d_m: number;
  total_distance_km: number;
  elevation_gain_m: number;
  elevation_loss_m: number;
  max_elevation_m: number;
  min_elevation_m: number;
  start_time: string;
  end_time: string;
  total_time: DurationValue;
  moving_time: DurationValue;
  stopped_time: DurationValue;
  avg_speed_kmh: number;
  avg_moving_speed_kmh: number;
  max_speed_kmh: number;
  avg_pace: string;
  avg_moving_pace: string;
  point_count: number;
  segment_count: number;
  points_per_km: number;
  stop_count: number;
  total_stop_time: DurationValue;
  avg_stop_duration: DurationValue;
  longest_stop?: StopInfo;
  stops?: StopInfo[];
  heart_rate?: HeartRateStats;
  power?: PowerStats;
  cadence?: CadenceStats;
  temperature?: TemperatureStats;
}

export interface DurationValue {
  display: string;
  seconds: number;
}

export interface StopInfo {
  start_time: string;
  end_time: string;
  duration: DurationValue;
  lat: number;
  lon: number;
}

export interface HeartRateStats {
  avg_bpm: number;
  max_bpm: number;
  min_bpm: number;
  zones?: HeartRateZone[];
}

export interface HeartRateZone {
  name: string;
  min_percent: number;
  max_percent: number;
  duration: DurationValue;
}

export interface PowerStats {
  avg_watts: number;
  max_watts: number;
  normalized_power_watts: number;
}

export interface CadenceStats {
  avg_rpm: number;
  max_rpm: number;
}

export interface TemperatureStats {
  avg_celsius: number;
  min_celsius: number;
  max_celsius: number;
}

export interface TrackReport {
  difficulty: {
    grade: string;
    score: number;
    justification: string;
  };
  key_segments: {
    type: string;
    description: string;
    elevation_change?: number;
    distance_km?: number;
  }[];
  recommendations: string[];
  summary: string;
  effort: {
    fitness_level: string;
    estimated_duration: string;
    calorie_estimate?: number;
  };
}

export interface DashboardSummary {
  totalActivities: number;
  totalDistanceKm: number;
  totalElevationGainM: number;
  totalMovingTimeSeconds: number;
  activitiesThisMonth: number;
  distanceThisMonthKm: number;
  recentActivities: ActivityListItem[];
  activityTypeBreakdown: Record<string, number>;
}

export interface IntegrationInfo {
  provider: string;
  isConnected: boolean;
  externalUserId?: string;
  connectedAt?: string;
}

export interface AppSettings {
  analysis: AnalysisSettings;
  aiProvider: AiProviderSettings;
  integrations: IntegrationCredentials;
}

export interface AnalysisSettings {
  preset: string;
  smoothing: string;
  trackSmoothing: string;
  elevationAlgorithm: string;
}

export interface AiProviderSettings {
  name: string;
  apiKey: string;
  hasApiKey: boolean;
  model: string;
  endpoint: string;
  availableProviders: string[];
}

export interface IntegrationCredentials {
  strava: StravaCredentials;
  garmin: GarminCredentials;
}

export interface StravaCredentials {
  clientId: string;
  hasClientSecret: boolean;
  clientSecret: string;
}

export interface GarminCredentials {
  consumerKey: string;
  hasConsumerSecret: boolean;
  consumerSecret: string;
}

export const ACTIVITY_COLORS: Record<string, string> = {
  run: '#00d4ff',
  trail: '#00ff88',
  hike: '#88ff00',
  cycle: '#ff8800',
  walk: '#aa88ff',
  swim: '#0088ff',
  other: '#888888',
};

export const ACTIVITY_LABELS: Record<string, string> = {
  run: 'Running',
  trail: 'Trail',
  hike: 'Hiking',
  cycle: 'Cycling',
  walk: 'Walking',
  swim: 'Swimming',
  other: 'Other',
};
