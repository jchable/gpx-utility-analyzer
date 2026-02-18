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
  effort?: EffortStats;
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

export interface EffortStats {
  naismith_time: DurationValue;
  tobler_time: DurationValue;
  munter_time: DurationValue;
  performance_ratio_naismith: number;
  performance_ratio_tobler: number;
  kilometre_effort: number;
  itra_points: number;
  itra_category: string;
  equivalent_flat_distance_km: number;
  terrain_difficulty: TerrainDifficulty;
}

export interface TerrainDifficulty {
  score: number;
  grade: string;
  avg_grade_percent: number;
  max_grade_percent: number;
  grade_variance: number;
  steep_section_ratio: number;
  elevation_per_km: number;
}

export interface PredictResult {
  stats: GpxStats;
  profile: ProfilePoint[] | null;
  track: { type: string; coordinates: number[][] } | null;
}

export interface ProfilePoint {
  distance: number;     // km cumulative
  elevation: number;    // metres
  speed: number;        // km/h smoothed
  gap: number;          // km/h GAP smoothed
  grade: number;        // percentage smoothed
  elapsedTime?: number; // seconds since start
  heartRate?: number;   // bpm
  cadence?: number;     // rpm
  power?: number;       // watts
  toblerSpeed?: number; // km/h theoretical Tobler speed
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
  athlete: AthleteSettings;
  analysis: AnalysisSettings;
  aiProvider: AiProviderSettings;
  integrations: IntegrationCredentials;
}

export interface AthleteSettings {
  maxHeartRate?: number;
  age?: number;
  ftp?: number;
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

export interface SplitsData {
  splits: SplitEntry[];
  bestEfforts: BestEffort[];
}

export interface SplitEntry {
  km: number;
  distance: number;
  paceSecondsPerKm: number;
  elevationGain: number;
  elevationLoss: number;
  avgHeartRate?: number;
  avgCadence?: number;
  avgPower?: number;
  avgSpeed?: number;
}

export interface BestEffort {
  label: string;
  distanceKm: number;
  timeSeconds?: number;
  paceSecondsPerKm?: number;
}

export interface ComputedZone {
  name: string;
  label: string;
  minPercent: number;
  maxPercent: number;
  minValue: number;
  maxValue: number;
  durationSeconds: number;
  color: string;
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

export const ACTIVITY_TYPES = ['run', 'trail', 'hike', 'cycle', 'walk', 'swim', 'other'] as const;
export type ActivityType = (typeof ACTIVITY_TYPES)[number];
