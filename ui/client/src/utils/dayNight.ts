import SunCalc from 'suncalc';
import type { RacePlanProfilePoint } from '../types/race-plan';

export interface DayNightSegment {
  fromDistanceKm: number;
  toDistanceKm: number;
  isNight: boolean;
}

/**
 * Computes day/night segments along the race profile.
 *
 * Strategy: for each profile point, estimate the elapsed time using linear
 * interpolation between the checkpoint arrival times (or Tobler-based speed
 * from the profile's toblerSpeed field). Then compare the real-world clock
 * time to the sunrise/sunset at the start location.
 *
 * @param profilePoints  500-pt downsampled profile (distance, elevation, toblerSpeed)
 * @param checkpoints    List of { distanceKm, targetArrivalSeconds } to anchor elapsed time
 * @param raceDate       Calendar date of the race (used for suncalc)
 * @param startTimeStr   "HH:mm" — departure time
 * @param startLat       Race start latitude (for suncalc)
 * @param startLon       Race start longitude (for suncalc)
 */
export function computeDayNightSegments(
  profilePoints: RacePlanProfilePoint[],
  checkpoints: { distanceKm: number; targetArrivalSeconds: number | null }[],
  raceDate: Date,
  startTimeStr: string,
  startLat: number,
  startLon: number,
): DayNightSegment[] {
  if (profilePoints.length < 2) return [];

  const [startHH, startMM] = startTimeStr.split(':').map(Number);
  const startDate = new Date(raceDate);
  startDate.setHours(startHH, startMM, 0, 0);

  // Build elapsed time anchors from checkpoints with known arrival times
  const anchors = checkpoints
    .filter((cp) => cp.targetArrivalSeconds !== null)
    .map((cp) => ({ distanceKm: cp.distanceKm, elapsedSec: cp.targetArrivalSeconds! }))
    .sort((a, b) => a.distanceKm - b.distanceKm);

  const totalDist = profilePoints[profilePoints.length - 1].distance;

  /**
   * Estimate elapsed seconds at a given distance using:
   * 1. Interpolation between known checkpoint anchors if available
   * 2. Tobler-based integration from the profile otherwise
   */
  function estimateElapsed(distKm: number): number {
    if (anchors.length >= 2) {
      // Find surrounding anchors
      let lo = anchors[0];
      let hi = anchors[anchors.length - 1];
      for (let i = 0; i < anchors.length - 1; i++) {
        if (anchors[i].distanceKm <= distKm && anchors[i + 1].distanceKm >= distKm) {
          lo = anchors[i];
          hi = anchors[i + 1];
          break;
        }
      }
      if (lo.distanceKm === hi.distanceKm) return lo.elapsedSec;
      const t = (distKm - lo.distanceKm) / (hi.distanceKm - lo.distanceKm);
      return lo.elapsedSec + t * (hi.elapsedSec - lo.elapsedSec);
    }

    // Fallback: integrate Tobler speed from profile
    let elapsed = 0;
    for (let i = 1; i < profilePoints.length; i++) {
      const prev = profilePoints[i - 1];
      const curr = profilePoints[i];
      if (curr.distance > distKm) break;
      const segKm = curr.distance - prev.distance;
      const avgSpeed = (prev.toblerSpeed + curr.toblerSpeed) / 2 || 4; // km/h
      elapsed += (segKm / avgSpeed) * 3600;
    }
    return elapsed;
  }

  // Compute a boolean isNight for each profile point
  const pointNight: boolean[] = profilePoints.map((pt) => {
    const elapsedSec = estimateElapsed(pt.distance);
    const realTime = new Date(startDate.getTime() + elapsedSec * 1000);
    const times = SunCalc.getTimes(realTime, startLat, startLon);
    return realTime < times.sunrise || realTime > times.sunset;
  });

  // Group consecutive points with same isNight into segments
  const segments: DayNightSegment[] = [];
  let segStart = 0;

  for (let i = 1; i <= profilePoints.length; i++) {
    const ended = i === profilePoints.length;
    const changed = !ended && pointNight[i] !== pointNight[segStart];

    if (ended || changed) {
      segments.push({
        fromDistanceKm: profilePoints[segStart].distance,
        toDistanceKm: profilePoints[i - 1].distance,
        isNight: pointNight[segStart],
      });
      segStart = i;
    }
  }

  // Clamp last segment to total distance
  if (segments.length > 0) {
    segments[segments.length - 1].toDistanceKm = totalDist;
  }

  return segments;
}

/**
 * Converts a wall-clock "HH:mm" cutoff into seconds since the race start.
 *
 * `dayOffset` is how many midnights the cutoff falls after the one implied by
 * the plain wall-clock reading. Minute-of-day arithmetic alone caps the result
 * at 24 h, which makes every day-2+ cutoff in an ultra unrepresentable.
 */
export function hhmmToElapsedSeconds(
  startTime: string,
  hhmm: string,
  dayOffset = 0,
): number | null {
  if (!hhmm) return null;

  const [hh, mm] = hhmm.split(':').map(Number);
  const [sh, sm] = (startTime || '00:00').split(':').map(Number);
  if ([hh, mm, sh, sm].some((n) => Number.isNaN(n))) return null;

  let diffMinutes = hh * 60 + mm - (sh * 60 + sm);
  if (diffMinutes < 0) diffMinutes += 24 * 60;

  return (diffMinutes + dayOffset * 24 * 60) * 60;
}

/** How many whole days after the start a given elapsed time falls on. */
export function elapsedSecondsToDayOffset(startTime: string, seconds: number): number {
  const [sh, sm] = (startTime || '00:00').split(':').map(Number);
  if (Number.isNaN(sh) || Number.isNaN(sm)) return 0;
  return Math.floor((sh * 60 + sm + seconds / 60) / (24 * 60));
}

/** Format a seconds-since-start value as "HH:mm" given a departure time string. */
export function formatArrivalTime(startTimeStr: string, elapsedSeconds: number): string {
  const [hh, mm] = startTimeStr.split(':').map(Number);
  const totalMinutes = hh * 60 + mm + Math.round(elapsedSeconds / 60);
  const h = Math.floor(totalMinutes / 60) % 24;
  const m = totalMinutes % 60;
  return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}`;
}

/** Format seconds as "+Xh Ym" or "Xh Ym" */
export function formatElapsedTime(seconds: number): string {
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  if (h === 0) return `${m}m`;
  if (m === 0) return `${h}h`;
  return `${h}h ${m}m`;
}

/** Format a delta in seconds as "+Xh Ym" or "-Xh Ym" */
export function formatDeltaTime(deltaSeconds: number): string {
  const sign = deltaSeconds >= 0 ? '+' : '-';
  const abs = Math.abs(deltaSeconds);
  return `${sign}${formatElapsedTime(abs)}`;
}
