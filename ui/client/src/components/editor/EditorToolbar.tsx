import { useTranslation } from 'react-i18next';
import {
  MousePointer2,
  Plus,
  Pencil,
  Scissors,
  Crop,
  ArrowLeftRight,
  Undo2,
  Redo2,
  MapPin,
} from 'lucide-react';
import { useEditorStore, type EditorMode } from '../../stores/editorStore';
import { useRouteStats } from '../../hooks/useRouteStats';
import type { RoutingProfile } from '../../types/route';
import { formatDurationCompact } from '../../utils/format';

const MODES: { id: EditorMode; icon: typeof MousePointer2; labelKey: string }[] = [
  { id: 'select', icon: MousePointer2, labelKey: 'editor.toolbar.select' },
  { id: 'addPoint', icon: Plus, labelKey: 'editor.toolbar.addPoint' },
  { id: 'freehand', icon: Pencil, labelKey: 'editor.toolbar.freehand' },
  { id: 'split', icon: Scissors, labelKey: 'editor.toolbar.split' },
  { id: 'crop', icon: Crop, labelKey: 'editor.toolbar.crop' },
  { id: 'addPoi', icon: MapPin, labelKey: 'editor.toolbar.addPoi' },
];

const ROUTING_PROFILES: { id: RoutingProfile; labelKey: string }[] = [
  { id: 'manual', labelKey: 'editor.routing.manual' },
  { id: 'hiking', labelKey: 'editor.routing.hiking' },
  { id: 'trail', labelKey: 'editor.routing.trail' },
  { id: 'cycling', labelKey: 'editor.routing.cycling' },
  { id: 'road', labelKey: 'editor.routing.road' },
];

export default function EditorToolbar() {
  const { t } = useTranslation('routes');

  const mode = useEditorStore((s) => s.mode);
  const routingProfile = useEditorStore((s) => s.routingProfile);
  const routeCoordinates = useEditorStore((s) => s.routeCoordinates);
  const setMode = useEditorStore((s) => s.setMode);
  const setRoutingProfile = useEditorStore((s) => s.setRoutingProfile);
  const reverseRoute = useEditorStore((s) => s.reverseRoute);

  const { undo, redo, pastStates, futureStates } = useEditorStore.temporal.getState();
  const canUndo = pastStates.length > 0;
  const canRedo = futureStates.length > 0;

  const stats = useRouteStats(routeCoordinates);

  return (
    <div className="absolute left-3 top-1/2 -translate-y-1/2 z-10 flex flex-col gap-2">
      {/* Mode buttons */}
      <div className="flex flex-col rounded-lg overflow-hidden border border-border bg-surface/90 backdrop-blur-sm shadow-lg">
        {MODES.map(({ id, icon: Icon, labelKey }) => {
          const isActive = mode === id;
          return (
            <button
              key={id}
              onClick={() => setMode(id)}
              title={t(labelKey)}
              className={`flex items-center justify-center w-10 h-10 transition-colors ${
                isActive
                  ? 'bg-accent/20 text-accent'
                  : 'text-content-muted hover:text-content hover:bg-surface-alt/30'
              } ${id !== 'select' ? 'border-t border-border' : ''}`}
            >
              <Icon size={18} />
            </button>
          );
        })}
      </div>

      {/* Reverse button */}
      <div className="flex flex-col rounded-lg overflow-hidden border border-border bg-surface/90 backdrop-blur-sm shadow-lg">
        <button
          onClick={reverseRoute}
          title={t('editor.toolbar.reverse')}
          className="flex items-center justify-center w-10 h-10 text-content-muted hover:text-content hover:bg-surface-alt/30 transition-colors"
        >
          <ArrowLeftRight size={18} />
        </button>
      </div>

      {/* Undo / Redo */}
      <div className="flex flex-col rounded-lg overflow-hidden border border-border bg-surface/90 backdrop-blur-sm shadow-lg">
        <button
          onClick={() => undo()}
          disabled={!canUndo}
          title={t('editor.undo')}
          className={`flex items-center justify-center w-10 h-10 transition-colors ${
            canUndo
              ? 'text-content-muted hover:text-content hover:bg-surface-alt/30'
              : 'text-content-muted/30 cursor-not-allowed'
          }`}
        >
          <Undo2 size={18} />
        </button>
        <button
          onClick={() => redo()}
          disabled={!canRedo}
          title={t('editor.redo')}
          className={`flex items-center justify-center w-10 h-10 border-t border-border transition-colors ${
            canRedo
              ? 'text-content-muted hover:text-content hover:bg-surface-alt/30'
              : 'text-content-muted/30 cursor-not-allowed'
          }`}
        >
          <Redo2 size={18} />
        </button>
      </div>

      {/* Routing profile */}
      <div className="flex flex-col rounded-lg overflow-hidden border border-border bg-surface/90 backdrop-blur-sm shadow-lg">
        {ROUTING_PROFILES.map(({ id, labelKey }, i) => {
          const isActive = routingProfile === id;
          return (
            <button
              key={id}
              onClick={() => setRoutingProfile(id)}
              title={t(labelKey)}
              className={`flex items-center justify-center px-2 h-8 text-[10px] font-medium transition-colors whitespace-nowrap ${
                isActive
                  ? 'bg-accent/20 text-accent'
                  : 'text-content-muted hover:text-content hover:bg-surface-alt/30'
              } ${i > 0 ? 'border-t border-border' : ''}`}
            >
              {t(labelKey)}
            </button>
          );
        })}
      </div>

      {/* Live stats */}
      {routeCoordinates.length >= 2 && (
        <div className="flex flex-col gap-1 rounded-lg border border-border bg-surface/90 backdrop-blur-sm shadow-lg px-2 py-2 text-[10px] text-content-muted">
          <div className="flex justify-between gap-2">
            <span>{t('stats.distance')}</span>
            <span className="text-content font-medium">{stats.distanceKm.toFixed(1)} km</span>
          </div>
          <div className="flex justify-between gap-2">
            <span>{t('stats.elevationGain')}</span>
            <span className="text-content font-medium">{stats.elevationGain} m</span>
          </div>
          <div className="flex justify-between gap-2">
            <span>{t('stats.elevationLoss')}</span>
            <span className="text-content font-medium">{stats.elevationLoss} m</span>
          </div>
          <div className="flex justify-between gap-2">
            <span>{t('stats.estimatedTime')}</span>
            <span className="text-content font-medium">
              {formatDurationCompact(stats.estimatedTimeSeconds)}
            </span>
          </div>
        </div>
      )}
    </div>
  );
}
