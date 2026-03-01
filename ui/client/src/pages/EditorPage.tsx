import { useEffect, useState, useCallback, useRef } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ArrowLeft, Save, Download, Mountain } from 'lucide-react';
import EditorMap from '../components/editor/EditorMap';
import EditorToolbar from '../components/editor/EditorToolbar';
import EditorElevationProfile from '../components/editor/EditorElevationProfile';
import MetadataPanel from '../components/editor/MetadataPanel';
import PoiPanel from '../components/editor/PoiPanel';
import SplitModal from '../components/editor/SplitModal';
import ExportModal from '../components/editor/ExportModal';
import { useEditorStore } from '../stores/editorStore';
import { useRoute, useUpdateRoute, useCreateRoute } from '../hooks/useRoutes';
import { useAutoSave } from '../hooks/useAutoSave';
import { useRoutingPreview } from '../hooks/useRoutingPreview';
import { routesApi } from '../api/routes-client';
import type { PoiType } from '../types/route';

export default function EditorPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { t } = useTranslation('routes');

  const [profileCollapsed, setProfileCollapsed] = useState(false);
  const [metadataCollapsed, setMetadataCollapsed] = useState(true);
  const [poiType, setPoiType] = useState<PoiType>('custom');
  const [saving, setSaving] = useState(false);
  const [enriching, setEnriching] = useState(false);
  const [splitIndex, setSplitIndex] = useState<number | null>(null);
  const [showExport, setShowExport] = useState(false);
  const isMountedRef = useRef(true);

  // Load route data if editing existing
  const { data: routeData, isLoading } = useRoute(id ?? '', { enabled: !!id });

  const updateMutation = useUpdateRoute();
  const createMutation = useCreateRoute();

  // Store selectors
  const routeId = useEditorStore((s) => s.routeId);
  const routeName = useEditorStore((s) => s.routeName);
  const isDirty = useEditorStore((s) => s.isDirty);
  const loadRoute = useEditorStore((s) => s.loadRoute);
  const reset = useEditorStore((s) => s.reset);

  // Auto-save & routing preview
  useAutoSave();
  useRoutingPreview();

  // Load route into store when data arrives
  useEffect(() => {
    if (routeData && id) {
      loadRoute({
        id: routeData.id,
        name: routeData.name,
        description: routeData.description ?? undefined,
        activityType: routeData.activityType,
        routeCategory: routeData.routeCategory,
        tags: routeData.tags ?? undefined,
        routingProfile: routeData.routingProfile,
        points: routeData.points,
        waypoints: routeData.waypoints,
        pois: routeData.pois,
      });
    }
  }, [routeData, id, loadRoute]);

  // Reset store on unmount
  useEffect(() => {
    isMountedRef.current = true;
    return () => {
      isMountedRef.current = false;
      reset();
    };
  }, [reset]);

  // --- Save handler ---
  const handleSave = useCallback(async () => {
    setSaving(true);
    try {
      const state = useEditorStore.getState();

      if (routeId) {
        // Update existing route
        await updateMutation.mutateAsync({
          id: routeId,
          data: {
            name: state.routeName || t('editor.title'),
            description: state.routeDescription,
            activityType: state.activityType,
            routeCategory: state.routeCategory,
            tags: state.tags,
            routingProfile: state.routingProfile,
            status: 'draft',
            points: state.routeCoordinates,
            waypoints: state.waypoints,
            pois: state.pois,
          },
        });
        useEditorStore.getState().markSaved();
      } else {
        // Create new route
        const created = await createMutation.mutateAsync({
          name: state.routeName || t('editor.title'),
          activityType: state.activityType,
        });

        // Then update with full data
        if (created?.id) {
          useEditorStore.getState().setRouteId(created.id);
          await updateMutation.mutateAsync({
            id: created.id,
            data: {
              name: state.routeName || t('editor.title'),
              description: state.routeDescription,
              activityType: state.activityType,
              routeCategory: state.routeCategory,
              tags: state.tags,
              routingProfile: state.routingProfile,
              status: 'draft',
              points: state.routeCoordinates,
              waypoints: state.waypoints,
              pois: state.pois,
            },
          });
          useEditorStore.getState().markSaved();

          // Update URL to include new ID
          if (isMountedRef.current) {
            navigate(`/editor/${created.id}`, { replace: true });
          }
        }
      }
    } catch (err) {
      console.error('Save failed:', err);
    } finally {
      if (isMountedRef.current) {
        setSaving(false);
      }
    }
  }, [routeId, updateMutation, createMutation, navigate, t]);

  // --- Discard handler ---
  const handleDiscard = useCallback(() => {
    if (isDirty && !window.confirm(t('editor.unsavedChanges'))) return;
    navigate('/routes');
  }, [isDirty, navigate, t]);

  // --- Elevation enrichment ---
  const handleEnrichElevation = useCallback(async () => {
    const currentId = useEditorStore.getState().routeId;
    if (!currentId) return;

    setEnriching(true);
    try {
      const enriched = await routesApi.enrichElevation(currentId);
      if (enriched.points) {
        useEditorStore.getState().setRouteCoordinates(enriched.points);
        useEditorStore.getState().markSaved();
      }
    } catch (err) {
      console.error('Elevation enrichment failed:', err);
    } finally {
      if (isMountedRef.current) {
        setEnriching(false);
      }
    }
  }, []);

  // --- Export handler ---
  const handleExport = useCallback(() => {
    const currentId = useEditorStore.getState().routeId;
    if (!currentId) {
      // Save first if not saved
      handleSave().then(() => {
        setShowExport(true);
      });
    } else {
      setShowExport(true);
    }
  }, [handleSave]);

  // --- Split mode handler (called by EditorMap) ---
  const handleSplitRequest = useCallback((index: number) => {
    setSplitIndex(index);
  }, []);

  // --- Create second route from split ---
  const handleCreateSecondRoute = useCallback(async (coords: number[][]) => {
    try {
      const state = useEditorStore.getState();
      const created = await routesApi.createRoute({
        name: `${state.routeName || t('editor.title')} (2)`,
        activityType: state.activityType,
      });
      if (created?.id) {
        await routesApi.updateRoute(created.id, {
          name: created.name,
          activityType: state.activityType,
          routeCategory: state.routeCategory,
          tags: state.tags,
          routingProfile: state.routingProfile,
          status: 'draft',
          points: coords,
        });
      }
    } catch (err) {
      console.error('Failed to create second route:', err);
    }
  }, [t]);

  // --- Keyboard shortcuts ---
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      const ctrl = e.ctrlKey || e.metaKey;

      // Ctrl+Z — undo
      if (ctrl && e.key === 'z' && !e.shiftKey) {
        e.preventDefault();
        useEditorStore.temporal.getState().undo();
      }

      // Ctrl+Y or Ctrl+Shift+Z — redo
      if (ctrl && (e.key === 'y' || (e.key === 'z' && e.shiftKey))) {
        e.preventDefault();
        useEditorStore.temporal.getState().redo();
      }

      // Ctrl+S — save
      if (ctrl && e.key === 's') {
        e.preventDefault();
        handleSave();
      }

      // Escape — deselect / back to select mode / close modals
      if (e.key === 'Escape') {
        if (splitIndex !== null) {
          setSplitIndex(null);
          return;
        }
        if (showExport) {
          setShowExport(false);
          return;
        }
        const state = useEditorStore.getState();
        if (state.selectedWaypointId || state.selectedPoiId) {
          useEditorStore.getState().selectWaypoint(null);
          useEditorStore.getState().selectPoi(null);
        } else if (state.mode !== 'select') {
          useEditorStore.getState().setMode('select');
        }
      }

      // Delete — delete selected waypoint or POI
      if (e.key === 'Delete' || e.key === 'Backspace') {
        const state = useEditorStore.getState();
        if (state.selectedWaypointId) {
          useEditorStore.getState().deleteWaypoint(state.selectedWaypointId);
        } else if (state.selectedPoiId) {
          useEditorStore.getState().deletePoi(state.selectedPoiId);
        }
      }
    };

    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [handleSave, splitIndex, showExport]);

  // --- Warn on unsaved changes before leaving ---
  useEffect(() => {
    const handler = (e: BeforeUnloadEvent) => {
      if (useEditorStore.getState().isDirty) {
        e.preventDefault();
      }
    };
    window.addEventListener('beforeunload', handler);
    return () => window.removeEventListener('beforeunload', handler);
  }, []);

  return (
    <div className="h-screen flex flex-col bg-[#0a0a1a] overflow-hidden">
      {/* Top bar */}
      <div className="flex items-center gap-3 px-4 py-2 bg-[#16213e] border-b border-white/5 shrink-0">
        <button
          onClick={handleDiscard}
          className="flex items-center gap-1 text-[#a0a0b0] hover:text-white transition-colors text-sm"
        >
          <ArrowLeft size={16} />
          <span className="hidden sm:inline">{t('editor.discard')}</span>
        </button>

        <div className="flex-1 min-w-0">
          <input
            type="text"
            value={routeName}
            onChange={(e) => useEditorStore.getState().setRouteName(e.target.value)}
            placeholder={t('editor.title')}
            className="bg-transparent text-white text-sm font-medium border-none outline-none w-full placeholder-[#a0a0b0]/50"
          />
        </div>

        {isDirty && (
          <span className="text-[10px] text-amber-400/80 shrink-0">{t('editor.unsavedChanges')}</span>
        )}

        <div className="flex items-center gap-2 shrink-0">
          {/* Elevation enrichment button (only when route is saved) */}
          {routeId && (
            <button
              onClick={handleEnrichElevation}
              disabled={enriching}
              className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-[#a0a0b0] hover:text-white hover:bg-white/5 rounded-lg transition-colors disabled:opacity-50"
              title={t('editor.enrichElevation')}
            >
              <Mountain size={14} />
              <span className="hidden lg:inline">
                {enriching ? t('editor.enrichingElevation') : t('editor.enrichElevation')}
              </span>
            </button>
          )}

          <button
            onClick={handleSave}
            disabled={saving}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium bg-[#00d4ff]/15 text-[#00d4ff] hover:bg-[#00d4ff]/25 rounded-lg transition-colors disabled:opacity-50"
          >
            <Save size={14} />
            {saving ? t('editor.saving') : t('editor.save')}
          </button>
          <button
            onClick={handleExport}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-[#a0a0b0] hover:text-white hover:bg-white/5 rounded-lg transition-colors"
          >
            <Download size={14} />
            {t('editor.export')}
          </button>
        </div>
      </div>

      {/* Main content: map + toolbar + metadata panel + POI panel */}
      <div className="flex-1 relative overflow-hidden">
        {isLoading ? (
          <div className="flex items-center justify-center h-full">
            <div className="flex flex-col items-center gap-3">
              <div className="w-8 h-8 border-2 border-[#00d4ff] border-t-transparent rounded-full animate-spin" />
              <span className="text-sm text-[#a0a0b0]">{t('editor.title')}</span>
            </div>
          </div>
        ) : (
          <>
            <EditorMap poiType={poiType} onSplitRequest={handleSplitRequest} />
            <EditorToolbar />
            <PoiPanel selectedPoiType={poiType} onPoiTypeChange={setPoiType} />
            <MetadataPanel
              collapsed={metadataCollapsed}
              onToggle={() => setMetadataCollapsed((v) => !v)}
            />
          </>
        )}
      </div>

      {/* Bottom: elevation profile */}
      <div className="shrink-0">
        <EditorElevationProfile
          collapsed={profileCollapsed}
          onToggle={() => setProfileCollapsed((v) => !v)}
        />
      </div>

      {/* Modals */}
      {splitIndex !== null && (
        <SplitModal
          splitIndex={splitIndex}
          onClose={() => setSplitIndex(null)}
          onCreateSecondRoute={handleCreateSecondRoute}
        />
      )}

      {showExport && routeId && (
        <ExportModal routeId={routeId} onClose={() => setShowExport(false)} />
      )}
    </div>
  );
}
