import { describe, it, expect } from 'vitest';
import { waypointOrderForRouteIndex, nearestVertexIndex } from './routeInsert';

/** A straight west-to-east polyline of `n` vertices from lon 0 to lon 1. */
function polyline(n: number): number[][] {
  return Array.from({ length: n }, (_, i) => [i / (n - 1), 45]);
}

describe('waypointOrderForRouteIndex', () => {
  it('maps a mid-route polyline index onto the enclosing waypoint pair', () => {
    // 4 waypoints spread over an 1,800-point routed polyline.
    const coords = polyline(1800);
    const waypoints = [
      { lat: 45, lon: 0.0, order: 0 },
      { lat: 45, lon: 0.333, order: 1 },
      { lat: 45, lon: 0.666, order: 2 },
      { lat: 45, lon: 1.0, order: 3 },
    ];

    // User clicks halfway between waypoints 1 and 2 -> polyline index ~900.
    const afterOrder = waypointOrderForRouteIndex(coords, waypoints, 900);

    // insertWaypoint gives the new point afterOrder + 1, so it must land
    // between waypoint 1 and waypoint 2 — i.e. afterOrder === 1.
    expect(afterOrder).toBe(1);
  });

  it('appends when the click is past the last waypoint', () => {
    const coords = polyline(1800);
    const waypoints = [
      { lat: 45, lon: 0.0, order: 0 },
      { lat: 45, lon: 0.5, order: 1 },
    ];
    expect(waypointOrderForRouteIndex(coords, waypoints, 1799)).toBe(1);
  });

  it('prepends when the click precedes the first waypoint', () => {
    const coords = polyline(1800);
    const waypoints = [
      { lat: 45, lon: 0.5, order: 0 },
      { lat: 45, lon: 1.0, order: 1 },
    ];
    expect(waypointOrderForRouteIndex(coords, waypoints, 10)).toBe(-1);
  });

  it('is identity-like in manual mode where the polyline IS the waypoint list', () => {
    const waypoints = [
      { lat: 45, lon: 0, order: 0 },
      { lat: 45, lon: 1, order: 1 },
      { lat: 45, lon: 2, order: 2 },
    ];
    const coords = waypoints.map((w) => [w.lon, w.lat]);
    expect(waypointOrderForRouteIndex(coords, waypoints, 1)).toBe(1);
  });

  it('returns -1 for an empty waypoint list', () => {
    expect(waypointOrderForRouteIndex(polyline(10), [], 5)).toBe(-1);
  });
});

describe('nearestVertexIndex', () => {
  it('finds the closest vertex', () => {
    expect(nearestVertexIndex(polyline(11), 0.5, 45)).toBe(5);
    expect(nearestVertexIndex(polyline(11), 0.0, 45)).toBe(0);
    expect(nearestVertexIndex(polyline(11), 1.0, 45)).toBe(10);
  });
});
