import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { SplitEntry, BestEffort } from '../../types/activity';

function formatPace(secondsPerKm: number): string {
  const m = Math.floor(secondsPerKm / 60);
  const s = Math.round(secondsPerKm % 60);
  return `${m}:${String(s).padStart(2, '0')}`;
}

function formatEffortTime(seconds: number): string {
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  const s = Math.round(seconds % 60);
  if (h > 0) return `${h}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
  return `${m}:${String(s).padStart(2, '0')}`;
}

/** Returns true for activity types that use pace (min:ss/km) rather than speed (km/h) */
function usesPace(activityType: string): boolean {
  return ['run', 'trail', 'hike', 'walk'].includes(activityType);
}

const COLLAPSE_THRESHOLD = 20;

interface Props {
  splits: SplitEntry[];
  bestEfforts: BestEffort[];
  activityType: string;
}

export default function SplitsSection({ splits, bestEfforts, activityType }: Props) {
  const { t } = useTranslation('activities');
  const { t: tc } = useTranslation();
  const [expanded, setExpanded] = useState(splits.length <= COLLAPSE_THRESHOLD);

  const isPace = usesPace(activityType);

  // Find fastest/slowest for highlighting
  const paces = splits.map((s) => s.paceSecondsPerKm);
  const fastestPace = Math.min(...paces);
  const slowestPace = Math.max(...paces);
  const paceRange = slowestPace - fastestPace;

  const hasHR = splits.some((s) => s.avgHeartRate != null);
  const hasPower = splits.some((s) => s.avgPower != null);

  return (
    <div className="bg-surface-card rounded-2xl border border-border">
      <button
        onClick={() => setExpanded((v) => !v)}
        className="w-full flex items-center justify-between p-6 text-left cursor-pointer hover:bg-surface-alt/30 transition-colors rounded-2xl"
      >
        <div className="flex items-center gap-3">
          <svg className="w-6 h-6 text-accent" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 17V7m0 10a2 2 0 01-2 2H5a2 2 0 01-2-2V7a2 2 0 012-2h2a2 2 0 012 2m0 10a2 2 0 002 2h2a2 2 0 002-2M9 7a2 2 0 012-2h2a2 2 0 012 2m0 10V7m0 10a2 2 0 002 2h2a2 2 0 002-2V7a2 2 0 00-2-2h-2a2 2 0 00-2 2" />
          </svg>
          <h2 className="text-xl font-semibold text-content">
            {t('splits.title')}
          </h2>
          <span className="text-sm text-content-muted">{splits.length} {tc('unit.km')}</span>
        </div>
        <svg
          className={`w-5 h-5 text-content-muted transition-transform duration-200 ${expanded ? 'rotate-180' : ''}`}
          fill="none"
          stroke="currentColor"
          viewBox="0 0 24 24"
        >
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
        </svg>
      </button>

      {expanded && (
        <div className="px-6 pb-6 space-y-6">
          {/* Splits table */}
          {splits.length > 0 && (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-content-muted text-xs uppercase border-b border-border">
                    <th className="text-left py-2 px-2 w-12">{t('splits.km')}</th>
                    <th className="text-left py-2 px-2">{isPace ? t('splits.pace') : t('splits.speed')}</th>
                    <th className="text-left py-2 px-2 w-24"></th>
                    <th className="text-right py-2 px-2">{t('splits.elevGain')}</th>
                    <th className="text-right py-2 px-2">{t('splits.elevLoss')}</th>
                    {hasHR && <th className="text-right py-2 px-2">{t('splits.avgHR')}</th>}
                    {hasPower && <th className="text-right py-2 px-2">{t('splits.avgPower')}</th>}
                  </tr>
                </thead>
                <tbody>
                  {splits.map((split) => {
                    const isFastest = split.paceSecondsPerKm === fastestPace;
                    const isSlowest = split.paceSecondsPerKm === slowestPace;
                    const barPct = paceRange > 0
                      ? ((slowestPace - split.paceSecondsPerKm) / paceRange) * 100
                      : 50;
                    const barColor = isFastest ? 'var(--accent-green)' : isSlowest ? 'var(--accent-red)' : 'var(--accent)';

                    return (
                      <tr
                        key={split.km}
                        className={`border-b border-border ${isFastest ? 'bg-green-500/5' : isSlowest ? 'bg-red-500/5' : ''}`}
                      >
                        <td className="py-2.5 px-2 text-content-muted font-mono">{split.km}</td>
                        <td className="py-2.5 px-2">
                          <span className={`font-medium ${isFastest ? 'text-green-400' : isSlowest ? 'text-red-400' : 'text-content'}`}>
                            {isPace
                              ? `${formatPace(split.paceSecondsPerKm)} /km`
                              : `${split.avgSpeed?.toFixed(1)} ${tc('unit.kmh')}`}
                          </span>
                        </td>
                        <td className="py-2.5 px-2">
                          <div className="bg-surface-alt rounded-full h-2 w-full">
                            <div
                              className="h-2 rounded-full transition-all"
                              style={{ width: `${Math.max(barPct, 5)}%`, backgroundColor: barColor }}
                            />
                          </div>
                        </td>
                        <td className="py-2.5 px-2 text-right text-green-400 text-xs">
                          {split.elevationGain > 0 ? `+${Math.round(split.elevationGain)}` : '0'} {tc('unit.m')}
                        </td>
                        <td className="py-2.5 px-2 text-right text-red-400 text-xs">
                          {split.elevationLoss > 0 ? `-${Math.round(split.elevationLoss)}` : '0'} {tc('unit.m')}
                        </td>
                        {hasHR && (
                          <td className="py-2.5 px-2 text-right text-content text-xs">
                            {split.avgHeartRate != null ? `${Math.round(split.avgHeartRate)}` : '—'} {split.avgHeartRate != null && tc('unit.bpm')}
                          </td>
                        )}
                        {hasPower && (
                          <td className="py-2.5 px-2 text-right text-content text-xs">
                            {split.avgPower != null ? `${Math.round(split.avgPower)}` : '—'} {split.avgPower != null && tc('unit.watts')}
                          </td>
                        )}
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}

          {/* Best Efforts */}
          {bestEfforts.length > 0 && (
            <div>
              <h3 className="text-sm font-medium text-content-muted mb-3">{t('splits.bestEfforts')}</h3>
              <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3">
                {bestEfforts.map((effort) => {
                  const available = effort.timeSeconds != null;
                  return (
                    <div
                      key={effort.label}
                      className={`rounded-xl p-4 border ${available ? 'bg-surface-alt/50 border-border' : 'bg-surface-alt/30 border-border opacity-50'}`}
                    >
                      <p className="text-xs text-content-muted mb-1">{effort.label}</p>
                      {available ? (
                        <>
                          <p className="text-lg font-bold text-content">{formatEffortTime(effort.timeSeconds!)}</p>
                          <p className="text-xs text-content-muted">
                            {isPace
                              ? `${formatPace(effort.paceSecondsPerKm!)} /km`
                              : `${(effort.distanceKm / (effort.timeSeconds! / 3600)).toFixed(1)} ${tc('unit.kmh')}`}
                          </p>
                        </>
                      ) : (
                        <p className="text-sm text-content-muted/70">{t('splits.notAvailable')}</p>
                      )}
                    </div>
                  );
                })}
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
