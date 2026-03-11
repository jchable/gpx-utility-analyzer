import type maplibregl from 'maplibre-gl';
import type { MapView } from '../components/map/MapViewSwitcher';

/** Retrieve MapTiler API key from env or DOM data attribute */
export function getMapTilerKey(): string {
  if (typeof import.meta !== 'undefined' && import.meta.env?.VITE_MAPTILER_KEY) {
    return import.meta.env.VITE_MAPTILER_KEY as string;
  }
  const root = document.getElementById('root');
  if (root?.dataset.maptilerKey) {
    return root.dataset.maptilerKey;
  }
  return '';
}

/** Compute bounding box from coordinate array */
export function computeBounds(coords: number[][]): maplibregl.LngLatBoundsLike {
  let minLon = Infinity,
    minLat = Infinity,
    maxLon = -Infinity,
    maxLat = -Infinity;

  for (const [lon, lat] of coords) {
    if (lon < minLon) minLon = lon;
    if (lon > maxLon) maxLon = lon;
    if (lat < minLat) minLat = lat;
    if (lat > maxLat) maxLat = lat;
  }

  return [
    [minLon, minLat],
    [maxLon, maxLat],
  ];
}

/** Get map style URL or specification for a given view */
export function getStyleUrl(view: MapView, key: string): string | maplibregl.StyleSpecification {
  switch (view) {
    case '3d-terrain':
      return `https://api.maptiler.com/maps/outdoor-v2/style.json?key=${key}`;
    case '3d-satellite':
      return `https://api.maptiler.com/maps/hybrid/style.json?key=${key}`;
    case '2d-topo':
      return {
        version: 8,
        sources: {
          topo: {
            type: 'raster',
            tiles: ['https://tile.opentopomap.org/{z}/{x}/{y}.png'],
            tileSize: 256,
          },
        },
        layers: [{ id: 'topo', type: 'raster', source: 'topo' }],
      };
  }
}
