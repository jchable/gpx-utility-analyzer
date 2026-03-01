import { useEffect, useRef, useCallback } from 'react';
import { useEditorStore } from '../stores/editorStore';
import { routesApi } from '../api/routes-client';

const AUTO_SAVE_INTERVAL = 30_000; // 30 seconds

/**
 * Auto-saves the route every 30 seconds when dirty.
 * Only saves points, waypoints, and POIs (lightweight PATCH).
 */
export function useAutoSave() {
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const savingRef = useRef(false);

  const doAutoSave = useCallback(async () => {
    const state = useEditorStore.getState();

    if (!state.isDirty || !state.routeId || savingRef.current) return;

    savingRef.current = true;
    try {
      await routesApi.autoSaveRoute(state.routeId, {
        points: state.routeCoordinates,
        waypoints: state.waypoints,
        pois: state.pois,
      });
      useEditorStore.getState().markSaved();
    } catch (err) {
      console.error('Auto-save failed:', err);
    } finally {
      savingRef.current = false;
    }
  }, []);

  useEffect(() => {
    timerRef.current = setInterval(doAutoSave, AUTO_SAVE_INTERVAL);

    return () => {
      if (timerRef.current) {
        clearInterval(timerRef.current);
        timerRef.current = null;
      }
    };
  }, [doAutoSave]);

  return { autoSave: doAutoSave };
}
