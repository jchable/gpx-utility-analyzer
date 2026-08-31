import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { useEditorStore } from '../stores/editorStore';
import { routesApi } from '../api/routes-client';
import { performAutoSave, payloadIsStillCurrent } from './useAutoSave';

describe('payloadIsStillCurrent', () => {
  it('is true for identical payloads', () => {
    const a = { points: [[0, 0]], waypoints: [{ id: 'w1' }], pois: [] };
    expect(payloadIsStillCurrent(a, structuredClone(a))).toBe(true);
  });

  it('is false when a waypoint moved', () => {
    const sent = { points: [[0, 0]], waypoints: [{ id: 'w1', lat: 45 }], pois: [] };
    const now = { points: [[0, 0]], waypoints: [{ id: 'w1', lat: 46 }], pois: [] };
    expect(payloadIsStillCurrent(sent, now)).toBe(false);
  });
});

describe('auto-save dirty tracking', () => {
  beforeEach(() => {
    useEditorStore.setState({
      routeId: 'route-1',
      routeCoordinates: [[0, 45], [1, 45]],
      waypoints: [{ id: 'w1', lat: 45, lon: 0, order: 0 }],
      pois: [],
      isDirty: true,
    } as never);
  });

  afterEach(() => vi.restoreAllMocks());

  it('stays dirty when the store changes while the request is in flight', async () => {
    let release: () => void = () => {};
    const inFlight = new Promise<void>((r) => { release = r; });

    vi.spyOn(routesApi, 'autoSaveRoute').mockImplementation(async () => {
      // The user drags a waypoint mid-request.
      useEditorStore.setState({
        waypoints: [{ id: 'w1', lat: 46, lon: 0, order: 0 }],
        isDirty: true,
      } as never);
      await inFlight;
    });

    const pending = performAutoSave();
    release();
    await pending;

    // The moved waypoint was never sent, so the editor must still be dirty:
    // EditorPage's discard prompt and beforeunload guard both read isDirty.
    expect(useEditorStore.getState().isDirty).toBe(true);
  });

  it('clears isDirty when nothing changed during the request', async () => {
    vi.spyOn(routesApi, 'autoSaveRoute').mockResolvedValue(undefined as never);

    await performAutoSave();

    expect(useEditorStore.getState().isDirty).toBe(false);
  });

  it('sends exactly the snapshot taken before the await', async () => {
    const sent: unknown[] = [];
    vi.spyOn(routesApi, 'autoSaveRoute').mockImplementation(async (_id, data) => {
      sent.push(structuredClone(data));
      useEditorStore.setState({ routeCoordinates: [[9, 9]], isDirty: true } as never);
    });

    await performAutoSave();

    expect(sent).toEqual([
      {
        points: [[0, 45], [1, 45]],
        waypoints: [{ id: 'w1', lat: 45, lon: 0, order: 0 }],
        pois: [],
      },
    ]);
    // The coordinates written during the request were never sent, so the
    // editor must stay dirty.
    expect(useEditorStore.getState().isDirty).toBe(true);
  });
});
