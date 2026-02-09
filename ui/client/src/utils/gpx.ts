/** Raw parsed GPX track point with all available fields */
export interface GpxTrackPoint {
  lat: number;
  lon: number;
  ele: number;
  time: Date | null;
}

/** [lon, lat, ele] tuple for MapLibre GeoJSON compatibility */
export type Coordinate = [number, number, number];

/** A single data point ready for the elevation profile chart */
export interface ProfilePoint {
  distance: number;        // cumulative distance in km
  elevation: number;       // elevation in meters
  speed: number;           // speed in km/h (smoothed)
  gap: number;             // grade-adjusted speed in km/h (smoothed)
  grade: number;           // grade percentage (smoothed)
  elapsedTime: number | null; // seconds since activity start, null if no timestamps
}

// ---------------------------------------------------------------------------
// GPX Parsing
// ---------------------------------------------------------------------------

/** Parse GPX XML extracting lat, lon, ele, and time from all <trkpt> elements */
export function parseGpxFull(xml: string): GpxTrackPoint[] {
  const parser = new DOMParser();
  const doc = parser.parseFromString(xml, 'application/xml');
  const points: GpxTrackPoint[] = [];
  const trkpts = doc.getElementsByTagName('trkpt');

  for (let i = 0; i < trkpts.length; i++) {
    const pt = trkpts[i];
    const lat = parseFloat(pt.getAttribute('lat') ?? '0');
    const lon = parseFloat(pt.getAttribute('lon') ?? '0');
    const eleNode = pt.getElementsByTagName('ele')[0];
    const ele = eleNode ? parseFloat(eleNode.textContent ?? '0') : 0;
    const timeNode = pt.getElementsByTagName('time')[0];
    const time = timeNode?.textContent ? new Date(timeNode.textContent) : null;
    points.push({ lat, lon, ele, time });
  }

  return points;
}

/** Convert GpxTrackPoint[] to [lon, lat, ele] tuples for MapLibre */
export function toCoordinates(points: GpxTrackPoint[]): Coordinate[] {
  return points.map((p) => [p.lon, p.lat, p.ele]);
}

// ---------------------------------------------------------------------------
// Haversine distance (mirrors cli/internal/stats/distance.go)
// ---------------------------------------------------------------------------

const EARTH_RADIUS = 6371000; // meters

function toRad(deg: number): number {
  return (deg * Math.PI) / 180;
}

export function haversine(lat1: number, lon1: number, lat2: number, lon2: number): number {
  const dLat = toRad(lat2 - lat1);
  const dLon = toRad(lon2 - lon1);
  const a =
    Math.sin(dLat / 2) ** 2 +
    Math.cos(toRad(lat1)) * Math.cos(toRad(lat2)) * Math.sin(dLon / 2) ** 2;
  const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
  return EARTH_RADIUS * c;
}

// ---------------------------------------------------------------------------
// Smoothing & downsampling
// ---------------------------------------------------------------------------

/** Symmetric rolling average. Window shrinks at edges. */
export function rollingAverage(values: number[], windowSize: number): number[] {
  const half = Math.floor(windowSize / 2);
  const result = new Array<number>(values.length);

  for (let i = 0; i < values.length; i++) {
    const lo = Math.max(0, i - half);
    const hi = Math.min(values.length - 1, i + half);
    let sum = 0;
    for (let j = lo; j <= hi; j++) sum += values[j];
    result[i] = sum / (hi - lo + 1);
  }

  return result;
}

/** Uniform stride-based downsampling, always keeps first and last point. */
export function downsample<T>(data: T[], targetCount: number): T[] {
  if (data.length <= targetCount) return data;

  const result: T[] = [data[0]];
  const step = (data.length - 1) / (targetCount - 1);

  for (let i = 1; i < targetCount - 1; i++) {
    result.push(data[Math.round(i * step)]);
  }
  result.push(data[data.length - 1]);

  return result;
}

// ---------------------------------------------------------------------------
// Minetti metabolic cost model (2002)
// ---------------------------------------------------------------------------

/** Metabolic cost C(i) in J/kg/m, where i is grade as a fraction. */
export function minettiCost(i: number): number {
  const g = Math.max(-0.45, Math.min(0.45, i));
  const cost =
    155.4 * g ** 5 - 30.4 * g ** 4 - 43.3 * g ** 3 + 46.3 * g ** 2 + 19.5 * g + 3.6;
  return Math.max(cost, 0.1);
}

const C_FLAT = minettiCost(0); // 3.6 J/kg/m

// ---------------------------------------------------------------------------
// Profile computation pipeline
// ---------------------------------------------------------------------------

interface ProfileOptions {
  smoothingWindow?: number; // default: 15
  targetPoints?: number;    // default: 500
}

export function computeProfileData(
  points: GpxTrackPoint[],
  options?: ProfileOptions,
): ProfilePoint[] {
  const { smoothingWindow = 15, targetPoints = 500 } = options ?? {};

  if (points.length < 2) return [];

  // Step 1: cumulative distance, raw speed, raw grade
  const cumDist: number[] = [0];
  const rawSpeed: number[] = [0];
  const rawGrade: number[] = [0];

  for (let i = 1; i < points.length; i++) {
    const d = haversine(points[i - 1].lat, points[i - 1].lon, points[i].lat, points[i].lon);
    cumDist.push(cumDist[i - 1] + d);

    const t0 = points[i - 1].time;
    const t1 = points[i].time;
    if (t0 && t1) {
      const dt = (t1.getTime() - t0.getTime()) / 1000;
      rawSpeed.push(dt > 0 ? (d / dt) * 3.6 : 0);
    } else {
      rawSpeed.push(0);
    }

    const dEle = points[i].ele - points[i - 1].ele;
    rawGrade.push(d > 1 ? dEle / d : 0);
  }

  // Adapt smoothing window if track is very short
  const effectiveWindow = Math.min(smoothingWindow, Math.floor(points.length / 3));
  const eleWindow = Math.max(3, Math.floor(effectiveWindow / 3));

  // Step 2: smooth speed, grade, elevation
  const smoothSpeed = rollingAverage(rawSpeed, effectiveWindow);
  const smoothGrade = rollingAverage(rawGrade, effectiveWindow);
  const rawEle = points.map((p) => p.ele);
  const smoothEle = rollingAverage(rawEle, eleWindow);

  // Step 3: compute GAP then smooth
  const rawGap: number[] = new Array(points.length);
  for (let i = 0; i < points.length; i++) {
    const cost = minettiCost(smoothGrade[i]);
    rawGap[i] = smoothSpeed[i] * (cost / C_FLAT);
  }
  const smoothGap = rollingAverage(rawGap, effectiveWindow);

  // Step 4: elapsed time from first timestamped point
  const startTimeMs = points[0]?.time?.getTime() ?? null;
  const elapsedTimes: (number | null)[] = points.map((p) =>
    startTimeMs !== null && p.time !== null ? (p.time.getTime() - startTimeMs) / 1000 : null,
  );

  // Step 5: assemble full profile
  const fullProfile: ProfilePoint[] = points.map((_, i) => ({
    distance: Math.round((cumDist[i] / 1000) * 1000) / 1000, // 3 decimals km
    elevation: Math.round(smoothEle[i]),
    speed: Math.round(smoothSpeed[i] * 10) / 10,
    gap: Math.round(smoothGap[i] * 10) / 10,
    grade: Math.round(smoothGrade[i] * 1000) / 10, // fraction → percentage, 1 decimal
    elapsedTime: elapsedTimes[i],
  }));

  // Step 6: downsample
  return downsample(fullProfile, targetPoints);
}

/** Returns true if the profile data contains valid timestamp information */
export function profileHasTimestamps(data: ProfilePoint[]): boolean {
  return data.length > 0 && data[0].elapsedTime !== null;
}
