import { useRef, useEffect, useState, useCallback } from 'react';
import maplibregl from 'maplibre-gl';
import 'maplibre-gl/dist/maplibre-gl.css';
import MapViewSwitcher, { type MapView } from './MapViewSwitcher';

interface TrackMapProps {
  gpxUrl: string;
}

type Coordinate = [number, number, number]; // [lon, lat, ele]

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

function parseGpx(xml: string): Coordinate[] {
  const parser = new DOMParser();
  const doc = parser.parseFromString(xml, 'application/xml');
  const coords: Coordinate[] = [];

  const trkpts = doc.getElementsByTagName('trkpt');
  for (let i = 0; i < trkpts.length; i++) {
    const pt = trkpts[i];
    const lat = parseFloat(pt.getAttribute('lat') ?? '0');
    const lon = parseFloat(pt.getAttribute('lon') ?? '0');
    const eleNode = pt.getElementsByTagName('ele')[0];
    const ele = eleNode ? parseFloat(eleNode.textContent ?? '0') : 0;
    coords.push([lon, lat, ele]);
  }

  return coords;
}

function computeBounds(coords: Coordinate[]): maplibregl.LngLatBoundsLike {
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

export default function TrackMap({ gpxUrl }: TrackMapProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const mapRef = useRef<maplibregl.Map | null>(null);
  const coordsRef = useRef<Coordinate[]>([]);
  const [view, setView] = useState<MapView>('3d-terrain');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const addTrackLayer = useCallback((map: maplibregl.Map, coords: Coordinate[]) => {
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

  const setupTerrain = useCallback((map: maplibregl.Map, currentView: MapView, key: string) => {
    const is3d = currentView === '3d-terrain' || currentView === '3d-satellite';

    if (is3d && key) {
      if (!map.getSource('terrain-source')) {
        map.addSource('terrain-source', {
          type: 'raster-dem',
          url: `https://api.maptiler.com/tiles/terrain-rgb/tiles.json?key=${key}`,
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

  // Fetch GPX data
  useEffect(() => {
    let cancelled = false;

    async function fetchGpx() {
      setLoading(true);
      setError(null);
      try {
        const res = await fetch(gpxUrl);
        if (!res.ok) throw new Error(`Failed to fetch GPX: ${res.status}`);
        const xml = await res.text();
        const coords = parseGpx(xml);
        if (coords.length === 0) throw new Error('No track points found in GPX file');
        if (!cancelled) {
          coordsRef.current = coords;
          // If map is already loaded, add the track
          if (mapRef.current?.isStyleLoaded()) {
            addTrackLayer(mapRef.current, coords);
          }
          setLoading(false);
        }
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : 'Failed to load GPX');
          setLoading(false);
        }
      }
    }

    fetchGpx();
    return () => {
      cancelled = true;
    };
  }, [gpxUrl, addTrackLayer]);

  // Initialize and update the map
  useEffect(() => {
    if (!containerRef.current) return;

    const key = getMapTilerKey();
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

    map.addControl(new maplibregl.NavigationControl(), 'bottom-right');

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
    <div className="relative w-full h-full min-h-[400px] rounded-xl overflow-hidden border border-white/5 bg-[#0f0f1a]">
      <MapViewSwitcher current={view} onChange={setView} />
      <div ref={containerRef} className="w-full h-full min-h-[400px]" />

      {loading && (
        <div className="absolute inset-0 flex items-center justify-center bg-[#0f0f1a]/80 backdrop-blur-sm z-20">
          <div className="flex flex-col items-center gap-3">
            <div className="w-8 h-8 border-2 border-[#00d4ff] border-t-transparent rounded-full animate-spin" />
            <span className="text-sm text-[#a0a0b0]">Loading track...</span>
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
