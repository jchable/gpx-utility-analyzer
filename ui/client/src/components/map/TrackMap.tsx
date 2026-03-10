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
  /** When set, the map flies to this location (e.g. clicked stop). */
  focusedPoint?: { lat: number; lon: number } | null;
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
const MARKERS_SOURCE_ID = 'track-markers';
const START_BORDER_LAYER = 'start-marker-border';
const START_LAYER = 'start-marker';
const FINISH_LAYER = 'finish-marker';
const FINISH_IMAGE_ID = 'finish-checkered';

function createCheckeredImage(size: number): ImageData {
  const canvas = document.createElement('canvas');
  canvas.width = size;
  canvas.height = size;
  const ctx = canvas.getContext('2d')!;
  const center = size / 2;
  const outerR = size / 2;
  const innerR = outerR - 3; // white border width

  // White border circle
  ctx.beginPath();
  ctx.arc(center, center, outerR, 0, Math.PI * 2);
  ctx.fillStyle = '#ffffff';
  ctx.fill();

  // Clip to inner circle for checkered pattern
  ctx.save();
  ctx.beginPath();
  ctx.arc(center, center, innerR, 0, Math.PI * 2);
  ctx.clip();

  // Draw checkered grid
  const cells = 4;
  const cellSize = (innerR * 2) / cells;
  const startX = center - innerR;
  const startY = center - innerR;
  for (let row = 0; row < cells; row++) {
    for (let col = 0; col < cells; col++) {
      ctx.fillStyle = (row + col) % 2 === 0 ? '#000000' : '#ffffff';
      ctx.fillRect(startX + col * cellSize, startY + row * cellSize, cellSize, cellSize);
    }
  }
  ctx.restore();

  return ctx.getImageData(0, 0, size, size);
}

export default function TrackMap({
  coordinates,
  loading,
  error,
  focusedPoint,
}: TrackMapProps) {
  const { t } = useTranslation();
  const containerRef = useRef<HTMLDivElement>(null);
  const mapRef = useRef<maplibregl.Map | null>(null);
  const coordsRef = useRef<number[][]>([]);
  const styleReadyRef = useRef(false);
  const key = getMapTilerKey();
  const [view, setView] = useState<MapView>(key ? '3d-terrain' : '2d-topo');

  const addTrackLayer = useCallback((map: maplibregl.Map, coords: number[][]) => {
    // Clean up existing layers
    for (const layerId of [FINISH_LAYER, START_LAYER, START_BORDER_LAYER, TRACK_LAYER_ID]) {
      if (map.getLayer(layerId)) map.removeLayer(layerId);
    }
    for (const sourceId of [MARKERS_SOURCE_ID, TRACK_SOURCE_ID]) {
      if (map.getSource(sourceId)) map.removeSource(sourceId);
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

    // Add start & finish markers
    if (coords.length >= 2) {
      const startCoord = coords[0];
      const endCoord = coords[coords.length - 1];

      map.addSource(MARKERS_SOURCE_ID, {
        type: 'geojson',
        data: {
          type: 'FeatureCollection',
          features: [
            {
              type: 'Feature',
              properties: { marker: 'start' },
              geometry: { type: 'Point', coordinates: startCoord.slice(0, 2) },
            },
            {
              type: 'Feature',
              properties: { marker: 'finish' },
              geometry: { type: 'Point', coordinates: endCoord.slice(0, 2) },
            },
          ],
        },
      });

      // Start marker: white border circle
      map.addLayer({
        id: START_BORDER_LAYER,
        type: 'circle',
        source: MARKERS_SOURCE_ID,
        filter: ['==', ['get', 'marker'], 'start'],
        paint: {
          'circle-radius': 9,
          'circle-color': '#ffffff',
        },
      });

      // Start marker: green inner circle
      map.addLayer({
        id: START_LAYER,
        type: 'circle',
        source: MARKERS_SOURCE_ID,
        filter: ['==', ['get', 'marker'], 'start'],
        paint: {
          'circle-radius': 6,
          'circle-color': '#22c55e',
        },
      });

      // Finish marker: checkered image
      if (!map.hasImage(FINISH_IMAGE_ID)) {
        const canvas = createCheckeredImage(28);
        map.addImage(FINISH_IMAGE_ID, canvas, { pixelRatio: 1.5 });
      }

      map.addLayer({
        id: FINISH_LAYER,
        type: 'symbol',
        source: MARKERS_SOURCE_ID,
        filter: ['==', ['get', 'marker'], 'finish'],
        layout: {
          'icon-image': FINISH_IMAGE_ID,
          'icon-allow-overlap': true,
        },
      });
    }

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

    if (styleReadyRef.current) {
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
      styleReadyRef.current = false;
      mapRef.current.setStyle(style as string | maplibregl.StyleSpecification);

      mapRef.current.once('style.load', () => {
        styleReadyRef.current = true;
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
      styleReadyRef.current = true;
      setupTerrain(map, view, key);
      if (coordsRef.current.length > 0) {
        addTrackLayer(map, coordsRef.current);
      }
    });

    mapRef.current = map;

    return () => {
      map.remove();
      mapRef.current = null;
      styleReadyRef.current = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [view]);

  // Fly to focused point (e.g. clicked stop)
  useEffect(() => {
    if (!focusedPoint || !mapRef.current) return;
    mapRef.current.flyTo({
      center: [focusedPoint.lon, focusedPoint.lat],
      zoom: 15,
      duration: 1000,
    });
  }, [focusedPoint]);

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
