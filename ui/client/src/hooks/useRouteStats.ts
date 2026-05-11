import { useMemo } from 'react';
import length from '@turf/length';
import { lineString } from '@turf/helpers';

interface RouteStats {
  distanceKm: number;
  elevationGain: number;
  elevationLoss: number;
  maxElevation: number;
  minElevation: number;
  estimatedTimeSeconds: number;
}

/**
 * Compute live route stats from coordinates.
 * Coordinates are [lon, lat] or [lon, lat, ele] arrays.
 */
export function useRouteStats(coordinates: number[][]): RouteStats {
  return useMemo(() => {
    if (coordinates.length < 2) {
      return {
        distanceKm: 0,
        elevationGain: 0,
        elevationLoss: 0,
        maxElevation: 0,
        minElevation: 0,
        estimatedTimeSeconds: 0,
      };
    }

    // Distance via Turf (geodesic, in km)
    let distanceKm = 0;
    try {
      const line = lineString(coordinates);
      distanceKm = length(line, { units: 'kilometers' });
    } catch {
      distanceKm = 0;
    }

    // Elevation stats
    let elevationGain = 0;
    let elevationLoss = 0;
    let maxElevation = -Infinity;
    let minElevation = Infinity;
    const THRESHOLD = 3; // meters — ignore noise below threshold

    const hasElevation = coordinates.some((c) => c.length >= 3 && c[2] !== 0);

    if (hasElevation) {
      let lastSignificantEle = coordinates[0][2] ?? 0;

      for (const coord of coordinates) {
        const ele = coord[2] ?? 0;
        if (ele > maxElevation) maxElevation = ele;
        if (ele < minElevation) minElevation = ele;

        const diff = ele - lastSignificantEle;
        if (Math.abs(diff) >= THRESHOLD) {
          if (diff > 0) elevationGain += diff;
          else elevationLoss += Math.abs(diff);
          lastSignificantEle = ele;
        }
      }
    } else {
      maxElevation = 0;
      minElevation = 0;
    }

    // Tobler hiking function: v = 6 * exp(-3.5 * |slope + 0.05|) km/h
    let estimatedTimeSeconds = 0;
    if (hasElevation && distanceKm > 0) {
      for (let i = 1; i < coordinates.length; i++) {
        const prev = coordinates[i - 1];
        const curr = coordinates[i];

        // Approximate segment distance (use Haversine simplified)
        const dLat = (curr[1] - prev[1]) * (Math.PI / 180);
        const dLon = (curr[0] - prev[0]) * (Math.PI / 180);
        const a =
          Math.sin(dLat / 2) ** 2 +
          Math.cos(prev[1] * (Math.PI / 180)) *
            Math.cos(curr[1] * (Math.PI / 180)) *
            Math.sin(dLon / 2) ** 2;
        const segDistM = 6371000 * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));

        if (segDistM < 0.1) continue;

        const dEle = (curr[2] ?? 0) - (prev[2] ?? 0);
        const slope = dEle / segDistM;
        const speed = 6 * Math.exp(-3.5 * Math.abs(slope + 0.05)); // km/h
        const speedMs = speed / 3.6;

        if (speedMs > 0.1) {
          estimatedTimeSeconds += segDistM / speedMs;
        }
      }
    } else if (distanceKm > 0) {
      // Flat terrain default: 5 km/h
      estimatedTimeSeconds = (distanceKm / 5) * 3600;
    }

    return {
      distanceKm,
      elevationGain: Math.round(elevationGain),
      elevationLoss: Math.round(elevationLoss),
      maxElevation: maxElevation === -Infinity ? 0 : Math.round(maxElevation),
      minElevation: minElevation === Infinity ? 0 : Math.round(minElevation),
      estimatedTimeSeconds: Math.round(estimatedTimeSeconds),
    };
  }, [coordinates]);
}
