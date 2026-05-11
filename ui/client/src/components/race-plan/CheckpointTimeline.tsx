import { useTranslation } from 'react-i18next';
import { Pencil, Trash2, Users, Package, Clock, Flame, Droplets } from 'lucide-react';
import type { RacePlanDetail, RacePlanCheckpoint, RacePlanNutritionItem } from '../../types/race-plan';
import { formatArrivalTime, formatElapsedTime } from '../../utils/dayNight';
import { useDeleteCheckpoint } from '../../hooks/useRacePlans';
import { useRacePlanStore } from '../../stores/racePlanStore';

const CHECKPOINT_COLORS: Record<string, string> = {
  start: '#22c55e',
  finish: '#ef4444',
  aid_station: '#06b6d4',
  checkpoint: '#f59e0b',
  crew_only: '#a855f7',
};

const CHECKPOINT_ICONS: Record<string, string> = {
  start: '🟢',
  finish: '🔴',
  aid_station: '🔵',
  checkpoint: '🟡',
  crew_only: '🟣',
};

interface Props {
  plan: RacePlanDetail;
  readOnly?: boolean;
}

export default function CheckpointTimeline({ plan, readOnly }: Props) {
  const { t } = useTranslation('race-plans');
  const { t: tc } = useTranslation();
  const openCheckpointEditor = useRacePlanStore((s) => s.openCheckpointEditor);
  const deleteCheckpoint = useDeleteCheckpoint();
  const startTime = plan.startTime ?? '00:00';

  const sorted = [...plan.checkpoints].sort((a, b) => a.order - b.order);

  async function handleDelete(cp: RacePlanCheckpoint) {
    if (!confirm(t('checkpoint.confirmDelete'))) return;
    await deleteCheckpoint.mutateAsync({ planId: plan.id, checkpointId: cp.id });
  }

  // Compute cumulative kcal up to and including checkpoint at index i
  function getCumulativeKcal(upToIndex: number): number {
    const checkpointIds = new Set(sorted.slice(0, upToIndex + 1).map((c) => c.id));
    return plan.nutritionItems.reduce((sum, item) => {
      const kcal = (item.caloriesKcal ?? 0) * item.quantity;
      if (item.atCheckpointId && checkpointIds.has(item.atCheckpointId)) return sum + kcal;
      if (item.toCheckpointId && checkpointIds.has(item.toCheckpointId)) return sum + kcal;
      return sum;
    }, 0);
  }

  // Compute cumulative water (ml) up to and including checkpoint at index i
  function getCumulativeWaterML(upToIndex: number): number {
    const checkpointIds = new Set(sorted.slice(0, upToIndex + 1).map((c) => c.id));
    return plan.nutritionItems.reduce((sum, item: RacePlanNutritionItem) => {
      const inScope =
        (item.atCheckpointId && checkpointIds.has(item.atCheckpointId)) ||
        (item.toCheckpointId && checkpointIds.has(item.toCheckpointId));
      if (!inScope) return sum;
      if (item.unit === 'ml') return sum + item.quantity;
      return sum;
    }, 0);
  }

  // Compute cumulative D+ for each checkpoint from profile
  function getCumulativeGain(distKm: number): number {
    if (!plan.profile || plan.profile.length === 0) return 0;
    let gain = 0;
    for (let i = 1; i < plan.profile.length; i++) {
      if (plan.profile[i].distance > distKm) break;
      const diff = plan.profile[i].elevation - plan.profile[i - 1].elevation;
      if (diff > 0) gain += diff;
    }
    return Math.round(gain);
  }

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm min-w-[640px]">
        <thead>
          <tr className="border-b border-border text-content-muted text-xs uppercase tracking-wide">
            <th className="text-left py-2 px-3 font-medium">
              {t('checkpoint.name')}
            </th>
            <th className="text-right py-2 px-3 font-medium">km</th>
            <th className="text-right py-2 px-3 font-medium">D+</th>
            <th className="text-right py-2 px-3 font-medium">{t('timing.arrivalTime')}</th>
            <th className="text-right py-2 px-3 font-medium">{t('timing.elapsedTime')}</th>
            <th className="text-right py-2 px-3 font-medium">{t('timing.cutoffTime')}</th>
            <th className="text-right py-2 px-3 font-medium">{t('checkpoint.pause')}</th>
            <th className="text-right py-2 px-3 font-medium">
              <span className="flex items-center justify-end gap-1 text-orange-400">
                <Flame size={11} />{t('nutrition.cumulKcal')}
              </span>
            </th>
            <th className="text-right py-2 px-3 font-medium">
              <span className="flex items-center justify-end gap-1 text-blue-400">
                <Droplets size={11} />{t('nutrition.cumulWater')}
              </span>
            </th>
            <th className="py-2 px-3 font-medium text-center">Info</th>
            {!readOnly && <th className="py-2 px-3" />}
          </tr>
        </thead>
        <tbody>
          {sorted.map((cp, idx) => {
            const color = CHECKPOINT_COLORS[cp.type] ?? '#94a3b8';
            const icon = CHECKPOINT_ICONS[cp.type] ?? '⚪';
            const arrival = cp.targetArrivalSeconds != null
              ? formatArrivalTime(startTime, cp.targetArrivalSeconds)
              : '—';
            const elapsed = cp.targetArrivalSeconds != null
              ? formatElapsedTime(cp.targetArrivalSeconds)
              : '—';
            const cutoff = cp.cutoffTimeSeconds != null
              ? formatArrivalTime(startTime, cp.cutoffTimeSeconds)
              : '—';
            const pause = cp.plannedPauseSeconds != null && cp.plannedPauseSeconds > 0
              ? formatElapsedTime(cp.plannedPauseSeconds)
              : '—';

            // Warning: arrival close to cutoff
            const isLate = cp.targetArrivalSeconds != null && cp.cutoffTimeSeconds != null &&
              cp.targetArrivalSeconds > cp.cutoffTimeSeconds - 600;

            return (
              <tr
                key={cp.id}
                className="border-b border-border/50 hover:bg-surface-alt/20 transition-colors group"
              >
                {/* Name */}
                <td className="py-3 px-3">
                  <div className="flex items-center gap-2">
                    <span className="text-base">{icon}</span>
                    <div>
                      <span className="text-content font-medium">{cp.name}</span>
                      <span
                        className="ml-2 text-xs px-1.5 py-0.5 rounded"
                        style={{ backgroundColor: color + '22', color }}
                      >
                        {t(`checkpoint.types.${cp.type}`)}
                      </span>
                    </div>
                  </div>
                </td>

                {/* Distance */}
                <td className="py-3 px-3 text-right text-content-muted font-mono text-xs">
                  {cp.distanceKm.toFixed(1)}
                </td>

                {/* Cumulative D+ */}
                <td className="py-3 px-3 text-right text-cyan-400 font-mono text-xs">
                  +{getCumulativeGain(cp.distanceKm)}
                </td>

                {/* Arrival time */}
                <td className="py-3 px-3 text-right">
                  <span className={`font-mono text-sm font-semibold ${isLate ? 'text-red-400' : 'text-content'}`}>
                    {arrival}
                  </span>
                </td>

                {/* Elapsed */}
                <td className="py-3 px-3 text-right text-content-muted font-mono text-xs">
                  {elapsed}
                </td>

                {/* Cutoff */}
                <td className="py-3 px-3 text-right">
                  {cp.cutoffTimeSeconds != null ? (
                    <span className={`font-mono text-xs ${isLate ? 'text-red-400 font-semibold' : 'text-content-muted'}`}>
                      {cutoff}
                    </span>
                  ) : (
                    <span className="text-content-muted/40 text-xs">—</span>
                  )}
                </td>

                {/* Pause */}
                <td className="py-3 px-3 text-right">
                  {cp.plannedPauseSeconds != null && cp.plannedPauseSeconds > 0 ? (
                    <span className="flex items-center justify-end gap-1 text-amber-400 font-mono text-xs">
                      <Clock size={11} />
                      {pause}
                    </span>
                  ) : (
                    <span className="text-content-muted/40 text-xs">—</span>
                  )}
                </td>

                {/* Cumulative kcal */}
                <td className="py-3 px-3 text-right">
                  {(() => {
                    const kcal = getCumulativeKcal(idx);
                    return kcal > 0 ? (
                      <span className="font-mono text-xs text-orange-400">{Math.round(kcal)}</span>
                    ) : (
                      <span className="text-content-muted/40 text-xs">—</span>
                    );
                  })()}
                </td>

                {/* Cumulative water */}
                <td className="py-3 px-3 text-right">
                  {(() => {
                    const ml = getCumulativeWaterML(idx);
                    return ml > 0 ? (
                      <span className="font-mono text-xs text-blue-400">{(ml / 1000).toFixed(1)} L</span>
                    ) : (
                      <span className="text-content-muted/40 text-xs">—</span>
                    );
                  })()}
                </td>

                {/* Crew/drop bag icons */}
                <td className="py-3 px-3 text-center">
                  <div className="flex items-center justify-center gap-1.5">
                    {cp.isCrewAccessible && (
                      <span title={tc('button.crew', { defaultValue: 'Crew' })}>
                        <Users size={14} className="text-purple-400" />
                      </span>
                    )}
                    {cp.hasDropBag && (
                      <span title={t('checkpoint.dropBag')}>
                        <Package size={14} className="text-amber-400" />
                      </span>
                    )}
                  </div>
                </td>

                {/* Actions */}
                {!readOnly && (
                  <td className="py-3 px-3">
                    <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity justify-end">
                      <button
                        onClick={() => openCheckpointEditor(cp.id)}
                        className="p-1.5 rounded-lg text-content-muted hover:text-accent hover:bg-accent/10 transition-colors"
                      >
                        <Pencil size={13} />
                      </button>
                      {cp.type !== 'start' && cp.type !== 'finish' && (
                        <button
                          onClick={() => handleDelete(cp)}
                          className="p-1.5 rounded-lg text-content-muted hover:text-red-400 hover:bg-red-900/20 transition-colors"
                        >
                          <Trash2 size={13} />
                        </button>
                      )}
                    </div>
                  </td>
                )}
              </tr>
            );
          })}
        </tbody>
      </table>

      {/* Add checkpoint CTA */}
      {!readOnly && (
        <div className="mt-4">
          <button
            onClick={() => openCheckpointEditor(null)}
            className="w-full py-2 border border-dashed border-border text-content-muted hover:text-content hover:border-content-muted/40 rounded-lg text-sm transition-colors flex items-center justify-center gap-2"
          >
            <span className="text-lg">+</span>
            {t('checkpoint.add')}
          </button>
        </div>
      )}
    </div>
  );
}
