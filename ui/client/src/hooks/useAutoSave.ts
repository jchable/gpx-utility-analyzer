import { useEffect, useRef, useCallback } from 'react';
import { useEditorStore } from '../stores/editorStore';
import { routesApi } from '../api/routes-client';
import type { RouteWaypoint, RoutePoi } from '../types/route';

const AUTO_SAVE_INTERVAL = 30_000; // 30 seconds

export interface AutoSavePayload {
  points: number[][];
  waypoints: RouteWaypoint[];
  pois: RoutePoi[];
}

/** The exact slice of editor state an auto-save PATCH carries. */
export function autoSavePayload(state: {
  routeCoordinates: number[][];
  waypoints: RouteWaypoint[];
  pois: RoutePoi[];
}): AutoSavePayload {
  return {
    points: state.routeCoordinates,
    waypoints: state.waypoints,
    pois: state.pois,
  };
}

/**
 * True when the store still holds exactly what was sent, i.e. nothing changed
 * while the request was in flight. Clearing isDirty unconditionally after the
 * await erases edits whose data was never sent, and because the next tick
 * returns early on !isDirty they are never saved at all.
 */
export function payloadIsStillCurrent<T>(sent: T, current: T): boolean {
  return JSON.stringify(sent) === JSON.stringify(current);
}

let saving = false;

export async function performAutoSave(): Promise<void> {
  const state = useEditorStore.getState();

  if (!state.isDirty || !state.routeId || saving) return;

  saving = true;
  const sent = autoSavePayload(state);
  try {
    await routesApi.autoSaveRoute(state.routeId, sent);

    const after = useEditorStore.getState();
    if (payloadIsStillCurrent(sent, autoSavePayload(after))) {
      after.markSaved();
    } else {
      // Something changed during the request: record the attempt but stay
      // dirty so the next tick — and the unsaved-changes guards — still fire.
      useEditorStore.setState({ lastAutoSave: new Date() });
    }
  } catch (err) {
    console.error('Auto-save failed:', err);
  } finally {
    saving = false;
  }
}

/**
 * Auto-saves the route every 30 seconds when dirty.
 * Only saves points, waypoints, and POIs (lightweight PATCH).
 */
export function useAutoSave() {
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const doAutoSave = useCallback(() => performAutoSave(), []);

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
