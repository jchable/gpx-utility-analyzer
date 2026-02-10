import { useRef, useEffect, useCallback, useState } from 'react';
import { useTranslation } from 'react-i18next';
import maplibregl from 'maplibre-gl';
import 'maplibre-gl/dist/maplibre-gl.css';
import MapViewSwitcher, { type MapView } from './MapViewSwitcher';

interface TrackMapProps {
  /** Coordinates as [lon, lat] or [lon, lat, ele] arrays. */
  coordinates?: number[][];
  /** Loading state. */
  loading?: boolean;
  /** Error state. */
  error?: string | null;
}

function getMapTilerKey(): string {
  if (typeof import.meta !== 'undefined' && import.meta.env?.VITE_MAPTILER_KEY) {
    return import.meta.env.VITE_MAPTILER_KEY as string;
  }
  const root = document.getElementById('root');
  if (root?.dataset.maptilerKey) {
    return root.dataset.maptilerKey;
  }
  return '';
}

function computeBounds(coords: number[][]): maplibregl.LngLatBoundsLike {
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

function getStyleUrl(view: MapView, key: string): string | maplibregl.StyleSpecification {
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

const TRACK_SOURCE_ID = 'gpx-track';
const TRACK_LAYER_ID = 'gpx-track-line';

export default function TrackMap({
  coordinates,
  loading,
  error,
}: TrackMapProps) {
  const { t } = useTranslation();
  const containerRef = useRef<HTMLDivElement>(null);
  const mapRef = useRef<maplibregl.Map | null>(null);
  const coordsRef = useRef<number[][]>([]);
  const key = getMapTilerKey();
  const [view, setView] = useState<MapView>(key ? '3d-terrain' : '2d-topo');

  const addTrackLayer = useCallback((map: maplibregl.Map, coords: number[][]) => {
    if (map.getSource(TRACK_SOURCE_ID)) {
      map.removeLayer(TRACK_LAYER_ID);
      map.removeSource(TRACK_SOURCE_ID);
    }

    map.addSource(TRACK_SOURCE_ID, {
      type: 'geojson',
      data: {
        type: 'Feature',
        properties: {},
        geometry: {
          type: 'LineString',
          coordinates: coords,
        },
      },
    });

    map.addLayer({
      id: TRACK_LAYER_ID,
      type: 'line',
      source: TRACK_SOURCE_ID,
      layout: {
        'line-join': 'round',
        'line-cap': 'round',
      },
      paint: {
        'line-color': '#00d4ff',
        'line-width': 3,
      },
    });

    if (coords.length > 0) {
      const bounds = computeBounds(coords);
      map.fitBounds(bounds, { padding: 60, duration: 1000 });
    }
  }, []);

  const setupTerrain = useCallback((map: maplibregl.Map, currentView: MapView, maptilerKey: string) => {
    const is3d = currentView === '3d-terrain' || currentView === '3d-satellite';

    if (is3d && maptilerKey) {
      if (!map.getSource('terrain-source')) {
        map.addSource('terrain-source', {
          type: 'raster-dem',
          url: `https://api.maptiler.com/tiles/terrain-rgb/tiles.json?key=${maptilerKey}`,
          tileSize: 256,
        });
      }
      map.setTerrain({ source: 'terrain-source', exaggeration: 1.5 });
      map.easeTo({ pitch: 60, duration: 800 });
    } else {
      map.setTerrain(undefined as unknown as maplibregl.TerrainSpecification);
      map.easeTo({ pitch: 0, duration: 800 });
    }
  }, []);

  // Handle externally-provided coordinates
  useEffect(() => {
    if (!coordinates || coordinates.length === 0) return;

    coordsRef.current = coordinates;
    const map = mapRef.current;
    if (!map) return;

    if (map.isStyleLoaded()) {
      addTrackLayer(map, coordinates);
    } else {
      map.once('style.load', () => addTrackLayer(map, coordinates));
    }
  }, [coordinates, addTrackLayer]);

  // Initialize and update the map
  useEffect(() => {
    if (!containerRef.current) return;

    const style = getStyleUrl(view, key);

    // If map already exists, change the style
    if (mapRef.current) {
      mapRef.current.setStyle(style as string | maplibregl.StyleSpecification);

      mapRef.current.once('style.load', () => {
        const map = mapRef.current;
        if (!map) return;
        setupTerrain(map, view, key);
        if (coordsRef.current.length > 0) {
          addTrackLayer(map, coordsRef.current);
        }
      });

      return;
    }

    // Create a new map
    const map = new maplibregl.Map({
      container: containerRef.current,
      style: style as string | maplibregl.StyleSpecification,
      center: [2.3, 46.5], // default center (France)
      zoom: 6,
      attributionControl: { compact: true },
    });

    map.addControl(new maplibregl.NavigationControl(), 'top-left');

    map.on('style.load', () => {
      setupTerrain(map, view, key);
      if (coordsRef.current.length > 0) {
        addTrackLayer(map, coordsRef.current);
      }
    });

    mapRef.current = map;

    return () => {
      map.remove();
      mapRef.current = null;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [view]);

  return (
    <div className="relative w-full h-full min-h-[250px] sm:min-h-[400px] rounded-xl overflow-hidden border border-white/5 bg-[#0f0f1a]">
      <MapViewSwitcher current={view} onChange={setView} />
      <div ref={containerRef} className="w-full h-full min-h-[250px] sm:min-h-[400px]" />

      {loading && (
        <div className="absolute inset-0 flex items-center justify-center bg-[#0f0f1a]/80 backdrop-blur-sm z-20">
          <div className="flex flex-col items-center gap-3">
            <div className="w-8 h-8 border-2 border-[#00d4ff] border-t-transparent rounded-full animate-spin" />
            <span className="text-sm text-[#a0a0b0]">{t('map.loadingTrack')}</span>
          </div>
        </div>
      )}

      {error && (
        <div className="absolute inset-0 flex items-center justify-center bg-[#0f0f1a]/80 backdrop-blur-sm z-20">
          <div className="flex flex-col items-center gap-2 px-6 py-4 bg-[#16213e] rounded-xl border border-[#ff4444]/30">
            <span className="text-sm text-[#ff4444]">{error}</span>
          </div>
        </div>
      )}
    </div>
  );
}
