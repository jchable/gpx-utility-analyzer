import { useRef, useEffect, useCallback, useState } from 'react';
import { useTranslation } from 'react-i18next';
import maplibregl from 'maplibre-gl';
import 'maplibre-gl/dist/maplibre-gl.css';
import {
  TerraDraw,
  TerraDrawLineStringMode,
  TerraDrawPointMode,
  TerraDrawSelectMode,
  TerraDrawRenderMode,
} from 'terra-draw';
import { TerraDrawMapLibreGLAdapter } from 'terra-draw-maplibre-gl-adapter';
import nearestPointOnLine from '@turf/nearest-point-on-line';
import { lineString, point } from '@turf/helpers';
import MapViewSwitcher, { type MapView } from '../map/MapViewSwitcher';
import { useEditorStore } from '../../stores/editorStore';
import type { EditorMode } from '../../stores/editorStore';
import type { PoiType } from '../../types/route';
import { getMapTilerKey, computeBounds, getStyleUrl } from '../../utils/map-helpers';
import { waypointOrderForRouteIndex } from '../../utils/routeInsert';
import { poiColorMatchExpression } from '../../constants/poi';

// --- Layer IDs ---
const ROUTE_SOURCE = 'editor-route';
const ROUTE_LAYER = 'editor-route-line';
const HOVER_SOURCE = 'editor-hover-point';
const HOVER_LAYER = 'editor-hover-point-layer';
const WAYPOINTS_SOURCE = 'editor-waypoints';
const WAYPOINTS_LAYER = 'editor-waypoints-layer';
const POIS_SOURCE = 'editor-pois';
const POIS_LAYER = 'editor-pois-layer';

// --- Terra Draw mode ↔ editor mode mapping ---
const TERRA_MODE_MAP: Record<string, string> = {
  addPoint: 'linestring',
  freehand: 'linestring', // we use linestring mode with freehand-like UX
  addPoi: 'point',
  select: 'select',
};

interface EditorMapProps {
  /** Currently selected POI type for addPoi mode */
  poiType?: PoiType;
  /** Called when user clicks in split mode — passes the split index */
  onSplitRequest?: (splitIndex: number) => void;
}

export default function EditorMap({ poiType = 'custom', onSplitRequest }: EditorMapProps) {
  const { t } = useTranslation();
  const containerRef = useRef<HTMLDivElement>(null);
  const mapRef = useRef<maplibregl.Map | null>(null);
  const drawRef = useRef<TerraDraw | null>(null);
  const styleReadyRef = useRef(false);
  const key = getMapTilerKey();
  const [view, setView] = useState<MapView>(key ? '3d-terrain' : '2d-topo');

  // Zustand selectors
  const mode = useEditorStore((s) => s.mode);
  const routeCoordinates = useEditorStore((s) => s.routeCoordinates);
  const waypoints = useEditorStore((s) => s.waypoints);
  const pois = useEditorStore((s) => s.pois);
  const hoveredPointIndex = useEditorStore((s) => s.hoveredPointIndex);
  const addWaypoint = useEditorStore((s) => s.addWaypoint);
  const moveWaypoint = useEditorStore((s) => s.moveWaypoint);
  const insertWaypoint = useEditorStore((s) => s.insertWaypoint);
  const addPoi = useEditorStore((s) => s.addPoi);
  const movePoi = useEditorStore((s) => s.movePoi);
  const setHoveredPointIndex = useEditorStore((s) => s.setHoveredPointIndex);
  const setRouteCoordinates = useEditorStore((s) => s.setRouteCoordinates);

  // --- Terrain setup ---
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

  // --- Add/update GeoJSON layers ---
  const updateRouteLayers = useCallback((map: maplibregl.Map) => {
    const coords = useEditorStore.getState().routeCoordinates;
    const wps = useEditorStore.getState().waypoints;
    const poiList = useEditorStore.getState().pois;

    // Route line
    const routeGeoJson: GeoJSON.Feature = {
      type: 'Feature',
      properties: {},
      geometry: {
        type: 'LineString',
        coordinates: coords.length >= 2 ? coords : [[0, 0], [0, 0]],
      },
    };

    if (map.getSource(ROUTE_SOURCE)) {
      (map.getSource(ROUTE_SOURCE) as maplibregl.GeoJSONSource).setData(routeGeoJson);
    } else {
      map.addSource(ROUTE_SOURCE, { type: 'geojson', data: routeGeoJson });
      map.addLayer({
        id: ROUTE_LAYER,
        type: 'line',
        source: ROUTE_SOURCE,
        layout: { 'line-join': 'round', 'line-cap': 'round' },
        paint: { 'line-color': '#00d4ff', 'line-width': 3, 'line-opacity': coords.length >= 2 ? 1 : 0 },
      });
    }

    // Update opacity based on real data
    if (map.getLayer(ROUTE_LAYER)) {
      map.setPaintProperty(ROUTE_LAYER, 'line-opacity', coords.length >= 2 ? 1 : 0);
    }

    // Waypoints as circles
    const wpGeoJson: GeoJSON.FeatureCollection = {
      type: 'FeatureCollection',
      features: wps.map((wp) => ({
        type: 'Feature' as const,
        properties: { id: wp.id, order: wp.order },
        geometry: { type: 'Point' as const, coordinates: [wp.lon, wp.lat] },
      })),
    };

    if (map.getSource(WAYPOINTS_SOURCE)) {
      (map.getSource(WAYPOINTS_SOURCE) as maplibregl.GeoJSONSource).setData(wpGeoJson);
    } else {
      map.addSource(WAYPOINTS_SOURCE, { type: 'geojson', data: wpGeoJson });
      map.addLayer({
        id: WAYPOINTS_LAYER,
        type: 'circle',
        source: WAYPOINTS_SOURCE,
        paint: {
          'circle-radius': 7,
          'circle-color': '#ffffff',
          'circle-stroke-color': '#00d4ff',
          'circle-stroke-width': 2,
        },
      });
    }

    // POIs as circles with type-based colors
    const poiGeoJson: GeoJSON.FeatureCollection = {
      type: 'FeatureCollection',
      features: poiList.map((p) => ({
        type: 'Feature' as const,
        properties: { id: p.id, type: p.type, name: p.name },
        geometry: { type: 'Point' as const, coordinates: [p.lon, p.lat] },
      })),
    };

    if (map.getSource(POIS_SOURCE)) {
      (map.getSource(POIS_SOURCE) as maplibregl.GeoJSONSource).setData(poiGeoJson);
    } else {
      map.addSource(POIS_SOURCE, { type: 'geojson', data: poiGeoJson });
      map.addLayer({
        id: POIS_LAYER,
        type: 'circle',
        source: POIS_SOURCE,
        paint: {
          'circle-radius': 8,
          'circle-color': poiColorMatchExpression(),
          'circle-stroke-color': '#ffffff',
          'circle-stroke-width': 2,
        },
      });
    }
  }, []);

  // --- Hover point layer (sync with elevation chart) ---
  const updateHoverPoint = useCallback((map: maplibregl.Map) => {
    const idx = useEditorStore.getState().hoveredPointIndex;
    const coords = useEditorStore.getState().routeCoordinates;

    const hasHover = idx !== null && idx >= 0 && idx < coords.length;
    const hoverGeoJson: GeoJSON.Feature = {
      type: 'Feature',
      properties: {},
      geometry: {
        type: 'Point',
        coordinates: hasHover ? coords[idx] : [0, 0],
      },
    };

    if (map.getSource(HOVER_SOURCE)) {
      (map.getSource(HOVER_SOURCE) as maplibregl.GeoJSONSource).setData(hoverGeoJson);
    } else {
      map.addSource(HOVER_SOURCE, { type: 'geojson', data: hoverGeoJson });
      map.addLayer({
        id: HOVER_LAYER,
        type: 'circle',
        source: HOVER_SOURCE,
        paint: {
          'circle-radius': 6,
          'circle-color': '#ff6b6b',
          'circle-stroke-color': '#ffffff',
          'circle-stroke-width': 2,
          'circle-opacity': hasHover ? 1 : 0,
          'circle-stroke-opacity': hasHover ? 1 : 0,
        },
      });
    }

    if (map.getLayer(HOVER_LAYER)) {
      map.setPaintProperty(HOVER_LAYER, 'circle-opacity', hasHover ? 1 : 0);
      map.setPaintProperty(HOVER_LAYER, 'circle-stroke-opacity', hasHover ? 1 : 0);
    }
  }, []);

  // --- Setup all GeoJSON layers after style loads ---
  const setupLayers = useCallback((map: maplibregl.Map) => {
    updateRouteLayers(map);
    updateHoverPoint(map);
  }, [updateRouteLayers, updateHoverPoint]);

  // --- Initialize Terra Draw ---
  const initTerraDraw = useCallback((map: maplibregl.Map) => {
    if (drawRef.current) {
      drawRef.current.stop();
      drawRef.current = null;
    }

    const draw = new TerraDraw({
      adapter: new TerraDrawMapLibreGLAdapter({ map, coordinatePrecision: 6 }),
      modes: [
        new TerraDrawLineStringMode(),
        new TerraDrawPointMode(),
        new TerraDrawSelectMode({
          flags: {
            linestring: {
              feature: { draggable: false, coordinates: { midpoints: false, draggable: true, deletable: true } },
            },
            point: {
              feature: { draggable: true, coordinates: { midpoints: false, draggable: false, deletable: false } },
            },
          },
        }),
        new TerraDrawRenderMode({ modeName: 'static', styles: {} }),
      ],
    });

    draw.start();
    draw.setMode('static'); // start in render-only mode

    // Listen for finished drawings
    draw.on('finish', (id, context) => {
      const feature = draw.getSnapshotFeature(id);
      if (!feature) return;

      if (feature.geometry.type === 'LineString' && context.action === 'draw') {
        // A linestring was drawn — extract coords and add as waypoints
        const coords = feature.geometry.coordinates;
        const state = useEditorStore.getState();

        if (state.mode === 'freehand' || state.mode === 'addPoint') {
          // Add each vertex as a waypoint
          const baseOrder = state.waypoints.length;
          coords.forEach((c, i) => {
            useEditorStore.getState().addWaypoint(c[1], c[0], baseOrder + i);
          });

          // In manual mode, set route coordinates directly from waypoints
          if (state.routingProfile === 'manual') {
            const allWps = useEditorStore.getState().waypoints;
            useEditorStore.getState().setRouteCoordinates(
              allWps.map((wp) => [wp.lon, wp.lat])
            );
          }
        }

        // Remove the terra-draw feature (we manage display via our own layers)
        draw.removeFeatures([id]);
      }

      if (feature.geometry.type === 'Point' && context.action === 'draw') {
        const [lon, lat] = feature.geometry.coordinates;
        const state = useEditorStore.getState();

        if (state.mode === 'addPoi') {
          useEditorStore.getState().addPoi(poiType, lat, lon);
        }

        draw.removeFeatures([id]);
      }
    });

    drawRef.current = draw;
    return draw;
  }, [poiType]);

  // --- Map click handler for addPoint/freehand/split/crop modes ---
  const handleMapClick = useCallback((e: maplibregl.MapMouseEvent) => {
    const state = useEditorStore.getState();
    const { lng, lat } = e.lngLat;

    if (state.mode === 'addPoint') {
      const coords = state.routeCoordinates;

      // If we have an existing route, try to insert near the closest point
      if (coords.length >= 2) {
        try {
          const line = lineString(coords);
          const pt = point([lng, lat]);
          const snapped = nearestPointOnLine(line, pt);
          const routeIndex = snapped.properties.index ?? coords.length - 1;

          // properties.index is an index into the rendered polyline, not a
          // waypoint order — insertWaypoint's third argument is an order.
          const afterOrder = waypointOrderForRouteIndex(
            coords,
            useEditorStore.getState().waypoints,
            routeIndex,
          );

          insertWaypoint(lat, lng, afterOrder);

          // Update route in manual mode
          if (state.routingProfile === 'manual') {
            const allWps = useEditorStore.getState().waypoints;
            setRouteCoordinates(allWps.map((wp) => [wp.lon, wp.lat]));
          }
          return;
        } catch {
          // Fallback: append at end
        }
      }

      // Append waypoint at the end
      addWaypoint(lat, lng);

      if (state.routingProfile === 'manual') {
        const allWps = useEditorStore.getState().waypoints;
        setRouteCoordinates(allWps.map((wp) => [wp.lon, wp.lat]));
      }
    }

    if (state.mode === 'addPoi') {
      addPoi(poiType, lat, lng);
    }

    if (state.mode === 'split') {
      const coords = state.routeCoordinates;
      if (coords.length < 3) return;

      try {
        const line = lineString(coords);
        const pt = point([lng, lat]);
        const snapped = nearestPointOnLine(line, pt);
        const splitIndex = snapped.properties.index;
        if (splitIndex !== undefined && splitIndex > 0 && splitIndex < coords.length - 1) {
          if (onSplitRequest) {
            onSplitRequest(splitIndex);
          } else {
            useEditorStore.getState().splitRouteAt(splitIndex);
          }
        }
      } catch {
        // ignore
      }
    }

    if (state.mode === 'crop') {
      // Crop is handled by crop start/end selection — see EditorToolbar
    }
  }, [addWaypoint, insertWaypoint, addPoi, setRouteCoordinates, poiType, onSplitRequest]);

  // --- Map mousemove handler for hover sync ---
  const handleMouseMove = useCallback((e: maplibregl.MapMouseEvent) => {
    const coords = useEditorStore.getState().routeCoordinates;
    if (coords.length < 2) return;

    try {
      const line = lineString(coords);
      const pt = point([e.lngLat.lng, e.lngLat.lat]);
      const snapped = nearestPointOnLine(line, pt);

      // Only highlight if close enough (< 50m away from line)
      const dist = snapped.properties.dist;
      if (dist !== undefined && dist < 0.05) {
        setHoveredPointIndex(snapped.properties.index ?? null);
      } else {
        setHoveredPointIndex(null);
      }
    } catch {
      // ignore
    }
  }, [setHoveredPointIndex]);

  // --- Waypoint drag handling via map events ---
  const dragStateRef = useRef<{ wpId: string; startLat: number; startLon: number } | null>(null);

  const handleWaypointDragStart = useCallback((e: maplibregl.MapMouseEvent) => {
    const map = mapRef.current;
    if (!map) return;

    const state = useEditorStore.getState();
    if (state.mode !== 'select') return;

    // Check if clicking on a waypoint
    const features = map.queryRenderedFeatures(e.point, { layers: [WAYPOINTS_LAYER] });
    if (features.length === 0) return;

    const wpId = features[0].properties?.id;
    if (!wpId) return;

    e.preventDefault();
    map.dragPan.disable();

    dragStateRef.current = { wpId, startLat: e.lngLat.lat, startLon: e.lngLat.lng };
    map.getCanvas().style.cursor = 'grabbing';
  }, []);

  const handleWaypointDrag = useCallback((e: maplibregl.MapMouseEvent) => {
    if (!dragStateRef.current) return;

    moveWaypoint(dragStateRef.current.wpId, e.lngLat.lat, e.lngLat.lng);

    const state = useEditorStore.getState();
    if (state.routingProfile === 'manual') {
      const allWps = useEditorStore.getState().waypoints;
      setRouteCoordinates(allWps.map((wp) => [wp.lon, wp.lat]));
    }
  }, [moveWaypoint, setRouteCoordinates]);

  const handleWaypointDragEnd = useCallback(() => {
    if (!dragStateRef.current) return;
    dragStateRef.current = null;

    const map = mapRef.current;
    if (map) {
      map.dragPan.enable();
      map.getCanvas().style.cursor = '';
    }
  }, []);

  // --- POI drag handling ---
  const poiDragRef = useRef<{ poiId: string } | null>(null);

  const handlePoiDragStart = useCallback((e: maplibregl.MapMouseEvent) => {
    const map = mapRef.current;
    if (!map) return;

    const state = useEditorStore.getState();
    if (state.mode !== 'select') return;

    const features = map.queryRenderedFeatures(e.point, { layers: [POIS_LAYER] });
    if (features.length === 0) return;

    const poiId = features[0].properties?.id;
    if (!poiId) return;

    // Don't start if we already have a waypoint drag
    if (dragStateRef.current) return;

    e.preventDefault();
    map.dragPan.disable();
    poiDragRef.current = { poiId };
    map.getCanvas().style.cursor = 'grabbing';
  }, []);

  const handlePoiDrag = useCallback((e: maplibregl.MapMouseEvent) => {
    if (!poiDragRef.current) return;
    movePoi(poiDragRef.current.poiId, e.lngLat.lat, e.lngLat.lng);
  }, [movePoi]);

  const handlePoiDragEnd = useCallback(() => {
    if (!poiDragRef.current) return;
    poiDragRef.current = null;

    const map = mapRef.current;
    if (map) {
      map.dragPan.enable();
      map.getCanvas().style.cursor = '';
    }
  }, []);

  // --- Combined mousedown/mousemove/mouseup handlers ---
  const handleMouseDown = useCallback((e: maplibregl.MapMouseEvent) => {
    handleWaypointDragStart(e);
    if (!dragStateRef.current) {
      handlePoiDragStart(e);
    }
  }, [handleWaypointDragStart, handlePoiDragStart]);

  const handleDrag = useCallback((e: maplibregl.MapMouseEvent) => {
    handleWaypointDrag(e);
    handlePoiDrag(e);
  }, [handleWaypointDrag, handlePoiDrag]);

  const handleDragEnd = useCallback(() => {
    handleWaypointDragEnd();
    handlePoiDragEnd();
  }, [handleWaypointDragEnd, handlePoiDragEnd]);

  // --- Map cursor based on editor mode ---
  useEffect(() => {
    const map = mapRef.current;
    if (!map) return;

    const cursorMap: Record<EditorMode, string> = {
      select: '',
      addPoint: 'crosshair',
      freehand: 'crosshair',
      split: 'crosshair',
      crop: 'crosshair',
      addPoi: 'crosshair',
    };

    map.getCanvas().style.cursor = cursorMap[mode] || '';
  }, [mode]);

  // --- Sync editor mode with Terra Draw ---
  useEffect(() => {
    const draw = drawRef.current;
    if (!draw) return;

    // For most modes, we use static (render-only) and handle interactions ourselves
    // via map click events. Terra Draw is used for select mode drag support.
    const terraMode = TERRA_MODE_MAP[mode];
    if (terraMode && draw.enabled) {
      try {
        draw.setMode(terraMode);
      } catch {
        draw.setMode('static');
      }
    } else if (draw.enabled) {
      draw.setMode('static');
    }
  }, [mode]);

  // --- Update layers when store data changes ---
  useEffect(() => {
    const map = mapRef.current;
    if (!map || !styleReadyRef.current) return;
    updateRouteLayers(map);
  }, [routeCoordinates, waypoints, pois, updateRouteLayers]);

  // --- Update hover point ---
  useEffect(() => {
    const map = mapRef.current;
    if (!map || !styleReadyRef.current) return;
    updateHoverPoint(map);
  }, [hoveredPointIndex, routeCoordinates, updateHoverPoint]);

  // --- Fit bounds when route coordinates first arrive ---
  const hasFittedRef = useRef(false);
  useEffect(() => {
    const map = mapRef.current;
    if (!map || routeCoordinates.length < 2 || hasFittedRef.current) return;
    hasFittedRef.current = true;
    const bounds = computeBounds(routeCoordinates);
    map.fitBounds(bounds, { padding: 60, duration: 1000 });
  }, [routeCoordinates]);

  // --- Initialize map ---
  useEffect(() => {
    if (!containerRef.current) return;

    const style = getStyleUrl(view, key);

    // If map already exists, change style
    if (mapRef.current) {
      styleReadyRef.current = false;
      mapRef.current.setStyle(style as string | maplibregl.StyleSpecification);

      mapRef.current.once('style.load', () => {
        styleReadyRef.current = true;
        const map = mapRef.current;
        if (!map) return;
        setupTerrain(map, view, key);
        setupLayers(map);
      });

      return;
    }

    // Create new map
    const map = new maplibregl.Map({
      container: containerRef.current,
      style: style as string | maplibregl.StyleSpecification,
      center: [2.3, 46.5],
      zoom: 6,
      attributionControl: { compact: true },
    });

    map.addControl(new maplibregl.NavigationControl(), 'top-left');

    map.on('style.load', () => {
      styleReadyRef.current = true;
      setupTerrain(map, view, key);
      setupLayers(map);
    });

    // Click handler for adding points/POIs/split
    map.on('click', handleMapClick);

    // Hover sync with elevation chart
    map.on('mousemove', handleMouseMove);

    // Drag handlers for waypoints and POIs
    map.on('mousedown', handleMouseDown);
    map.on('mousemove', handleDrag);
    map.on('mouseup', handleDragEnd);

    // Initialize Terra Draw
    map.once('load', () => {
      initTerraDraw(map);
    });

    mapRef.current = map;

    return () => {
      if (drawRef.current) {
        drawRef.current.stop();
        drawRef.current = null;
      }
      map.remove();
      mapRef.current = null;
      styleReadyRef.current = false;
      hasFittedRef.current = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [view]);

  return (
    <div className="relative w-full h-full min-h-[300px] bg-surface">
      <MapViewSwitcher current={view} onChange={setView} />
      <div ref={containerRef} className="w-full h-full" />

      {useEditorStore.getState().isRouteLoading && (
        <div className="absolute inset-0 flex items-center justify-center bg-surface/60 backdrop-blur-sm z-20 pointer-events-none">
          <div className="flex flex-col items-center gap-3">
            <div className="w-8 h-8 border-2 border-accent border-t-transparent rounded-full animate-spin" />
            <span className="text-sm text-content-muted">{t('map.loadingTrack')}</span>
          </div>
        </div>
      )}
    </div>
  );
}
