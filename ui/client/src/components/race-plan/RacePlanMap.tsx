import { useRef, useEffect } from 'react';
import maplibregl from 'maplibre-gl';
import 'maplibre-gl/dist/maplibre-gl.css';
import type { RacePlanDetail } from '../../types/race-plan';
import { getMapTilerKey, computeBounds, getStyleUrl } from '../../utils/map-helpers';
import { useRacePlanStore } from '../../stores/racePlanStore';

const CHECKPOINT_COLORS: Record<string, string> = {
  start: '#22c55e',
  finish: '#ef4444',
  aid_station: '#06b6d4',
  checkpoint: '#f59e0b',
  crew_only: '#a855f7',
};

interface Props {
  plan: RacePlanDetail;
}

export default function RacePlanMap({ plan }: Props) {
  const containerRef = useRef<HTMLDivElement>(null);
  const mapRef = useRef<maplibregl.Map | null>(null);
  const markersRef = useRef<maplibregl.Marker[]>([]);
  const key = getMapTilerKey();
  const hoveredKm = useRacePlanStore((s) => s.hoveredDistanceKm);

  // Initialize map
  useEffect(() => {
    if (!containerRef.current) return;
    const map = new maplibregl.Map({
      container: containerRef.current,
      style: getStyleUrl('2d-topo', key),
      center: [2.3522, 46.2276],
      zoom: 5,
    });
    mapRef.current = map;
    return () => {
      map.remove();
      mapRef.current = null;
    };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Draw track + checkpoints when plan changes
  useEffect(() => {
    const map = mapRef.current;
    if (!map || !plan.points || plan.points.length === 0) return;

    function draw() {
      if (!map) return;
      // Track layer
      if (map.getSource('raceplan-track')) {
        (map.getSource('raceplan-track') as maplibregl.GeoJSONSource).setData({
          type: 'Feature',
          properties: {},
          geometry: { type: 'LineString', coordinates: plan.points! },
        });
      } else {
        map.addSource('raceplan-track', {
          type: 'geojson',
          data: {
            type: 'Feature',
            properties: {},
            geometry: { type: 'LineString', coordinates: plan.points! },
          },
        });
        map.addLayer({
          id: 'raceplan-track-line',
          type: 'line',
          source: 'raceplan-track',
          paint: {
            'line-color': '#06b6d4',
            'line-width': 3,
            'line-opacity': 0.85,
          },
        });
      }

      // Fit to track
      if (plan.points && plan.points.length > 1) {
        map.fitBounds(computeBounds(plan.points!), { padding: 40, maxZoom: 14 });
      }

      // Markers for checkpoints
      markersRef.current.forEach((m) => m.remove());
      markersRef.current = [];

      plan.checkpoints.forEach((cp) => {
        if (cp.latitude == null || cp.longitude == null) return;
        const color = CHECKPOINT_COLORS[cp.type] ?? '#94a3b8';
        const el = document.createElement('div');
        el.style.cssText = `
          width: 10px; height: 10px;
          background: ${color};
          border: 2px solid white;
          border-radius: 50%;
          cursor: pointer;
          box-shadow: 0 0 4px rgba(0,0,0,0.5);
        `;

        const popup = new maplibregl.Popup({ offset: 12, closeButton: false })
          .setText(cp.name);

        const marker = new maplibregl.Marker({ element: el })
          .setLngLat([cp.longitude, cp.latitude])
          .setPopup(popup)
          .addTo(map!);
        markersRef.current.push(marker);
      });
    }

    if (map.isStyleLoaded()) {
      draw();
    } else {
      map.once('load', draw);
    }
  }, [plan]);

  // Hover indicator along track
  useEffect(() => {
    const map = mapRef.current;
    if (!map || !map.isStyleLoaded()) return;

    if (hoveredKm == null) {
      if (map.getSource('hover-point')) {
        (map.getSource('hover-point') as maplibregl.GeoJSONSource).setData({
          type: 'FeatureCollection',
          features: [],
        });
      }
      return;
    }

    // Find point at hoveredKm in plan.points
    if (!plan.points || plan.points.length === 0) return;

    // Simple linear search for closest point by cumulative distance
    let cumDist = 0;
    let found: number[] | null = null;
    for (let i = 1; i < plan.points.length; i++) {
      const prev = plan.points[i - 1];
      const curr = plan.points[i];
      const dx = (curr[0] - prev[0]) * Math.cos((prev[1] * Math.PI) / 180) * 111.32;
      const dy = (curr[1] - prev[1]) * 110.57;
      const segKm = Math.sqrt(dx * dx + dy * dy);
      if (cumDist + segKm >= hoveredKm) {
        const t = (hoveredKm - cumDist) / segKm;
        found = [prev[0] + t * (curr[0] - prev[0]), prev[1] + t * (curr[1] - prev[1])];
        break;
      }
      cumDist += segKm;
    }

    if (!found) found = plan.points[plan.points.length - 1].slice(0, 2);

    const geoData = {
      type: 'FeatureCollection' as const,
      features: [{ type: 'Feature' as const, properties: {}, geometry: { type: 'Point' as const, coordinates: found } }],
    };

    if (map.getSource('hover-point')) {
      (map.getSource('hover-point') as maplibregl.GeoJSONSource).setData(geoData);
    } else {
      map.addSource('hover-point', { type: 'geojson', data: geoData });
      map.addLayer({
        id: 'hover-point-circle',
        type: 'circle',
        source: 'hover-point',
        paint: {
          'circle-radius': 6,
          'circle-color': '#ef4444',
          'circle-stroke-color': 'white',
          'circle-stroke-width': 2,
        },
      });
    }
  }, [hoveredKm, plan.points]);

  return (
    <div ref={containerRef} className="w-full h-full rounded-xl overflow-hidden" />
  );
}
