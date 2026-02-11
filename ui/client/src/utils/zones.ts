import type { ProfilePoint, ComputedZone, AthleteSettings, PowerStats } from '../types/activity';

// ── HR Zone Definitions (5-zone standard model, % of MaxHR) ──

const HR_ZONE_DEFS = [
  { name: 'Z1', label: 'Recovery',  minPct: 50, maxPct: 60, color: '#94a3b8' },
  { name: 'Z2', label: 'Endurance', minPct: 60, maxPct: 70, color: '#3b82f6' },
  { name: 'Z3', label: 'Tempo',     minPct: 70, maxPct: 80, color: '#22c55e' },
  { name: 'Z4', label: 'Threshold', minPct: 80, maxPct: 90, color: '#f59e0b' },
  { name: 'Z5', label: 'VO2 Max',   minPct: 90, maxPct: 100, color: '#ef4444' },
];

// ── Power Zone Definitions (7-zone Coggan model, % of FTP) ──

const POWER_ZONE_DEFS = [
  { name: 'Z1', label: 'Active Recovery', minPct: 0,   maxPct: 55,  color: '#94a3b8' },
  { name: 'Z2', label: 'Endurance',       minPct: 55,  maxPct: 75,  color: '#3b82f6' },
  { name: 'Z3', label: 'Tempo',           minPct: 75,  maxPct: 90,  color: '#22c55e' },
  { name: 'Z4', label: 'Threshold',       minPct: 90,  maxPct: 105, color: '#f59e0b' },
  { name: 'Z5', label: 'VO2max',          minPct: 105, maxPct: 120, color: '#ef4444' },
  { name: 'Z6', label: 'Anaerobic',       minPct: 120, maxPct: 150, color: '#a855f7' },
  { name: 'Z7', label: 'Neuromuscular',   minPct: 150, maxPct: 999, color: '#ec4899' },
];

/**
 * Determine effective max HR from user settings with fallback to observed max.
 * Priority: user maxHeartRate > 220-age > observed max > null
 */
export function getEffectiveMaxHR(
  athlete: AthleteSettings | undefined,
  observedMaxHR: number | undefined,
): number | null {
  if (athlete?.maxHeartRate && athlete.maxHeartRate > 0) return athlete.maxHeartRate;
  if (athlete?.age && athlete.age > 0) return 220 - athlete.age;
  if (observedMaxHR && observedMaxHR > 0) return observedMaxHR;
  return null;
}

/**
 * Compute HR zone distribution from profile points and max HR.
 * Iterates consecutive points and uses elapsedTime deltas.
 */
export function computeHRZones(points: ProfilePoint[], maxHR: number): ComputedZone[] {
  const zones: ComputedZone[] = HR_ZONE_DEFS.map((z) => ({
    name: z.name,
    label: z.label,
    minPercent: z.minPct,
    maxPercent: z.maxPct,
    minValue: Math.round(maxHR * z.minPct / 100),
    maxValue: Math.round(maxHR * z.maxPct / 100),
    durationSeconds: 0,
    color: z.color,
  }));

  for (let i = 1; i < points.length; i++) {
    const hr = points[i].heartRate;
    const elapsed = points[i].elapsedTime;
    const prevElapsed = points[i - 1].elapsedTime;

    if (hr == null || elapsed == null || prevElapsed == null) continue;

    const dt = elapsed - prevElapsed;
    if (dt <= 0 || dt > 300) continue; // skip gaps > 5 min

    const pct = (hr / maxHR) * 100;

    for (const zone of zones) {
      if (pct >= zone.minPercent && (pct < zone.maxPercent || (zone.name === 'Z5' && pct >= zone.minPercent))) {
        zone.durationSeconds += dt;
        break;
      }
    }
  }

  return zones;
}

/**
 * Compute power zone distribution from profile points and FTP.
 */
export function computePowerZones(points: ProfilePoint[], ftp: number): ComputedZone[] {
  const zones: ComputedZone[] = POWER_ZONE_DEFS.map((z) => ({
    name: z.name,
    label: z.label,
    minPercent: z.minPct,
    maxPercent: z.maxPct,
    minValue: Math.round(ftp * z.minPct / 100),
    maxValue: z.maxPct === 999 ? Infinity : Math.round(ftp * z.maxPct / 100),
    durationSeconds: 0,
    color: z.color,
  }));

  for (let i = 1; i < points.length; i++) {
    const power = points[i].power;
    const elapsed = points[i].elapsedTime;
    const prevElapsed = points[i - 1].elapsedTime;

    if (power == null || elapsed == null || prevElapsed == null) continue;

    const dt = elapsed - prevElapsed;
    if (dt <= 0 || dt > 300) continue;

    const pct = (power / ftp) * 100;

    for (const zone of zones) {
      if (pct >= zone.minPercent && pct < zone.maxPercent) {
        zone.durationSeconds += dt;
        break;
      }
    }
  }

  return zones;
}

/**
 * TRIMP (Training Impulse) from HR zones.
 * Weights: Z1=1, Z2=1.6, Z3=2.4, Z4=3.2, Z5=4.0
 */
export function computeTRIMP(zones: ComputedZone[]): number {
  const weights = [1, 1.6, 2.4, 3.2, 4.0];
  let trimp = 0;
  for (let i = 0; i < zones.length && i < weights.length; i++) {
    trimp += (zones[i].durationSeconds / 60) * weights[i];
  }
  return Math.round(trimp);
}

/**
 * Advanced power metrics derived from NP and FTP.
 */
export function computePowerMetrics(
  stats: PowerStats,
  ftp: number,
  movingTimeSeconds: number,
) {
  const np = stats.normalized_power_watts;
  const intensityFactor = np / ftp;
  const tss = (movingTimeSeconds * np * intensityFactor) / (ftp * 3600) * 100;
  const variabilityIndex = stats.avg_watts > 0 ? np / stats.avg_watts : 0;
  return {
    normalizedPower: np,
    intensityFactor: Math.round(intensityFactor * 100) / 100,
    tss: Math.round(tss),
    variabilityIndex: Math.round(variabilityIndex * 100) / 100,
  };
}
