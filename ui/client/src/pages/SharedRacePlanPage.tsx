import { useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Flag, Calendar, Clock, TrendingUp } from 'lucide-react';
import { useRacePlanShared } from '../hooks/useRacePlans';
import ElevationWithCheckpoints from '../components/race-plan/ElevationWithCheckpoints';
import { formatArrivalTime, formatElapsedTime } from '../utils/dayNight';
import { formatDate } from '../utils/format';
import type { RacePlanDetail } from '../types/race-plan';

const CHECKPOINT_COLORS: Record<string, string> = {
  start: '#22c55e',
  finish: '#ef4444',
  aid_station: '#06b6d4',
  checkpoint: '#f59e0b',
  crew_only: '#a855f7',
};

export default function SharedRacePlanPage() {
  const { token } = useParams<{ token: string }>();
  const { t } = useTranslation('race-plans');
  const { i18n } = useTranslation();
  const { data: plan, isLoading, error } = useRacePlanShared(token!);

  if (isLoading) {
    return (
      <div className="min-h-screen bg-surface flex items-center justify-center">
        <div className="animate-spin rounded-full h-10 w-10 border-t-2 border-b-2 border-cyan-400" />
      </div>
    );
  }

  if (error || !plan) {
    return (
      <div className="min-h-screen bg-surface flex items-center justify-center p-4">
        <div className="bg-red-900/20 border border-red-800 rounded-xl p-6 max-w-sm text-center">
          <Flag className="w-12 h-12 mx-auto text-red-400 mb-3" />
          <p className="text-red-400 font-medium">Race plan not found</p>
          <p className="text-content-muted text-sm mt-1">The sharing link may have expired or been disabled.</p>
        </div>
      </div>
    );
  }

  const startTime = plan.startTime ?? '00:00';
  const sorted = [...plan.checkpoints].sort((a, b) => a.order - b.order);

  // Cast to a compatible shape for ElevationWithCheckpoints (shared plan has a subset of fields)
  const planForChart = {
    ...plan,
    id: '',
    status: 'ready' as const,
    language: 'en',
    routeId: null,
    maxElevationM: 0,
    minElevationM: 0,
    startLatitude: null,
    startLongitude: null,
    targetTimeBSeconds: null,
    targetTimeCSeconds: null,
    performanceCoefficient: 0.75,
    equipment: null,
    isPublic: true,
    shareToken: token ?? null,
    linkedActivityId: null,
    points: null,
    createdAt: '',
    updatedAt: '',
    nutritionItems: [],
    checkpoints: plan.checkpoints.map((cp) => ({
      ...cp,
      isCrewAccessible: cp.isCrewAccessible,
      crewNotes: cp.crewNotes,
      hasDropBag: false,
      dropBagContents: null,
      equipmentTake: null,
      equipmentLeave: null,
      notes: null,
    })),
  } as unknown as RacePlanDetail;

  return (
    <div className="min-h-screen bg-surface text-content">
      <div className="max-w-3xl mx-auto px-4 py-8 space-y-6">
        {/* Header */}
        <div className="flex items-start justify-between">
          <div>
            <h1 className="text-2xl font-bold text-content">{plan.name}</h1>
            {plan.description && <p className="text-content-muted mt-1">{plan.description}</p>}
            <div className="flex items-center gap-3 mt-2 text-sm text-content-muted">
              {plan.raceDate && (
                <span className="flex items-center gap-1.5">
                  <Calendar size={14} />
                  {formatDate(plan.raceDate, i18n.language)}
                </span>
              )}
              {plan.startTime && (
                <span className="flex items-center gap-1.5">
                  <Clock size={14} />
                  Départ {plan.startTime}
                </span>
              )}
            </div>
          </div>
          <div className="text-right text-sm text-content-muted">
            <div className="flex items-center gap-1.5 justify-end">
              <TrendingUp size={14} />
              <span>{plan.distanceKm.toFixed(1)} km</span>
            </div>
            <div className="text-cyan-400">+{Math.round(plan.elevationGainM)} m</div>
          </div>
        </div>

        {/* Elevation profile */}
        {plan.profile && plan.profile.length > 0 && (
          <div className="bg-surface-card border border-border rounded-xl p-4">
            <ElevationWithCheckpoints plan={planForChart} readOnly />
          </div>
        )}

        {/* Checkpoints table */}
        <div className="bg-surface-card border border-border rounded-xl overflow-hidden">
          <div className="px-4 py-3 border-b border-border">
            <h2 className="font-semibold text-content">{t('tabs.timeline')}</h2>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full text-sm min-w-[500px]">
              <thead>
                <tr className="border-b border-border text-content-muted text-xs uppercase tracking-wide">
                  <th className="text-left py-2.5 px-4">{t('checkpoint.name')}</th>
                  <th className="text-right py-2.5 px-3">km</th>
                  <th className="text-right py-2.5 px-3">{t('timing.arrivalTime')}</th>
                  <th className="text-right py-2.5 px-3">{t('timing.elapsedTime')}</th>
                  <th className="text-right py-2.5 px-3">{t('timing.cutoffTime')}</th>
                  <th className="text-right py-2.5 px-3">{t('checkpoint.pause')}</th>
                  <th className="py-2.5 px-3">Crew</th>
                </tr>
              </thead>
              <tbody>
                {sorted.map((cp) => {
                  const color = CHECKPOINT_COLORS[cp.type] ?? '#94a3b8';
                  return (
                    <tr key={cp.id} className="border-b border-border/50">
                      <td className="py-3 px-4">
                        <div className="font-medium" style={{ color }}>{cp.name}</div>
                        {cp.crewNotes && (
                          <div className="text-xs text-content-muted mt-0.5">{cp.crewNotes}</div>
                        )}
                      </td>
                      <td className="py-3 px-3 text-right text-content-muted font-mono text-xs">
                        {cp.distanceKm.toFixed(1)}
                      </td>
                      <td className="py-3 px-3 text-right font-mono text-sm font-semibold text-content">
                        {cp.targetArrivalSeconds != null ? formatArrivalTime(startTime, cp.targetArrivalSeconds) : '—'}
                      </td>
                      <td className="py-3 px-3 text-right text-content-muted font-mono text-xs">
                        {cp.targetArrivalSeconds != null ? formatElapsedTime(cp.targetArrivalSeconds) : '—'}
                      </td>
                      <td className="py-3 px-3 text-right text-content-muted font-mono text-xs">
                        {cp.cutoffTimeSeconds != null ? formatArrivalTime(startTime, cp.cutoffTimeSeconds) : '—'}
                      </td>
                      <td className="py-3 px-3 text-right text-content-muted font-mono text-xs">
                        {cp.plannedPauseSeconds != null && cp.plannedPauseSeconds > 0
                          ? formatElapsedTime(cp.plannedPauseSeconds)
                          : '—'}
                      </td>
                      <td className="py-3 px-3 text-center">
                        {cp.isCrewAccessible && (
                          <span className="text-purple-400 text-xs">✓</span>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>

        {/* Footer */}
        <p className="text-center text-xs text-content-muted/50">
          {t('share.publicView')} · GPX Analyzer
        </p>
      </div>
    </div>
  );
}
