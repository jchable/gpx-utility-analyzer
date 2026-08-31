export interface OrderedWaypoint {
  lat: number;
  lon: number;
  order: number;
}

/**
 * Index of the polyline vertex closest to (lon, lat).
 * Squared planar distance is enough: we only need the argmin, and over the span
 * of a single route the latitude scale factor is effectively constant.
 */
export function nearestVertexIndex(
  coordinates: number[][],
  lon: number,
  lat: number,
): number {
  let best = 0;
  let bestD = Infinity;
  for (let i = 0; i < coordinates.length; i++) {
    const dx = coordinates[i][0] - lon;
    const dy = coordinates[i][1] - lat;
    const d = dx * dx + dy * dy;
    if (d < bestD) {
      bestD = d;
      best = i;
    }
  }
  return best;
}

/**
 * Translates an index in the RENDERED POLYLINE space (what turf's
 * nearestPointOnLine returns) into the WAYPOINT ORDER space that
 * editorStore.insertWaypoint(lat, lon, afterOrder) expects.
 *
 * The two coincide only in manual mode right after a freehand draw. For a routed
 * or imported route the polyline has thousands of vertices and the waypoint list
 * has a handful, so passing the polyline index through unchanged always produced
 * an order beyond every existing waypoint — i.e. an append.
 *
 * Returns the order of the last waypoint at or before routeIndex, or
 * (lowest order - 1) when the click precedes the first waypoint.
 */
export function waypointOrderForRouteIndex(
  routeCoordinates: number[][],
  waypoints: OrderedWaypoint[],
  routeIndex: number,
): number {
  if (waypoints.length === 0) return -1;

  const sorted = [...waypoints].sort((a, b) => a.order - b.order);
  let result = sorted[0].order - 1;

  for (const wp of sorted) {
    const anchor = nearestVertexIndex(routeCoordinates, wp.lon, wp.lat);
    if (anchor <= routeIndex) result = wp.order;
    else break;
  }

  return result;
}
