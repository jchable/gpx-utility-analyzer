import { useEffect, useRef, useCallback } from 'react';
import { useEditorStore } from '../stores/editorStore';
import { routesApi } from '../api/routes-client';

const DEBOUNCE_MS = 300;

/**
 * Watches waypoints + routingProfile in the store.
 * When they change (and profile != 'manual'), debounces a call to the
 * routing preview API and writes the result into routeCoordinates.
 */
export function useRoutingPreview() {
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const abortRef = useRef<AbortController | null>(null);

  const fetchRoute = useCallback(async () => {
    const { waypoints, routingProfile, setRouteCoordinates, setIsRouteLoading } =
      useEditorStore.getState();

    if (routingProfile === 'manual' || waypoints.length < 2) return;

    // Cancel any in-flight request
    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;

    setIsRouteLoading(true);
    try {
      // Build waypoint array as [lat, lon] (API expects this order)
      const wps = waypoints
        .slice()
        .sort((a, b) => a.order - b.order)
        .map((wp) => [wp.lat, wp.lon]);

      const result = await routesApi.routingPreview(wps, routingProfile);

      // Only apply if not aborted
      if (!controller.signal.aborted) {
        setRouteCoordinates(result.coordinates);
        // Mark not dirty for this routing update (it's a derived value)
        // isDirty was already set by the waypoint change that triggered this
      }
    } catch (err) {
      if (!(err instanceof DOMException && err.name === 'AbortError')) {
        console.error('Routing preview failed:', err);
      }
    } finally {
      if (!controller.signal.aborted) {
        setIsRouteLoading(false);
      }
    }
  }, []);

  // Watch waypoints + routingProfile
  const waypoints = useEditorStore((s) => s.waypoints);
  const routingProfile = useEditorStore((s) => s.routingProfile);

  useEffect(() => {
    if (routingProfile === 'manual') return;
    if (waypoints.length < 2) return;

    if (timerRef.current) clearTimeout(timerRef.current);
    timerRef.current = setTimeout(fetchRoute, DEBOUNCE_MS);

    return () => {
      if (timerRef.current) clearTimeout(timerRef.current);
    };
  }, [waypoints, routingProfile, fetchRoute]);

  // Cleanup on unmount
  useEffect(() => {
    return () => {
      abortRef.current?.abort();
      if (timerRef.current) clearTimeout(timerRef.current);
    };
  }, []);
}
