import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import type { ComputedZone } from '../../types/activity';
import { formatDurationShort } from '../../utils/format';

interface PowerMetrics {
  normalizedPower: number;
  intensityFactor: number;
  tss: number;
  variabilityIndex: number;
}

interface Props {
  zones: ComputedZone[];
  ftp: number;
  metrics: PowerMetrics;
  avgWatts: number;
  maxWatts: number;
}

export default function PowerZonesSection({ zones, ftp, metrics, avgWatts, maxWatts }: Props) {
  const { t } = useTranslation('activities');
  const { t: tc } = useTranslation();

  const totalTime = zones.reduce((sum, z) => sum + z.durationSeconds, 0);
  const hasData = totalTime > 0;

  return (
    <div className="bg-surface-card rounded-2xl p-6 border border-border space-y-5">
      <div className="flex items-center justify-between flex-wrap gap-3">
        <h2 className="text-xl font-semibold text-content flex items-center gap-3">
          <svg className="w-6 h-6 text-yellow-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 10V3L4 14h7v7l9-11h-7z" />
          </svg>
          {t('powerZones.title')}
        </h2>
      </div>

      {/* Advanced metrics */}
      <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3">
        <div className="bg-surface-alt/50 rounded-xl p-3">
          <p className="text-xs text-content-muted mb-1">{t('powerZones.np')}</p>
          <p className="text-lg font-bold text-yellow-400">{metrics.normalizedPower} {tc('unit.watts')}</p>
        </div>
        <div className="bg-surface-alt/50 rounded-xl p-3">
          <p className="text-xs text-content-muted mb-1">{t('powerZones.avgPower')}</p>
          <p className="text-lg font-bold text-content">{avgWatts} {tc('unit.watts')}</p>
        </div>
        <div className="bg-surface-alt/50 rounded-xl p-3">
          <p className="text-xs text-content-muted mb-1">{t('powerZones.maxPower')}</p>
          <p className="text-lg font-bold text-content">{maxWatts} {tc('unit.watts')}</p>
        </div>
        <div className="bg-surface-alt/50 rounded-xl p-3">
          <p className="text-xs text-content-muted mb-1">{t('powerZones.if')}</p>
          <p className="text-lg font-bold text-content">{metrics.intensityFactor.toFixed(2)}</p>
        </div>
        <div className="bg-surface-alt/50 rounded-xl p-3">
          <p className="text-xs text-content-muted mb-1">{t('powerZones.tss')}</p>
          <p className="text-lg font-bold text-content">{metrics.tss}</p>
        </div>
        <div className="bg-surface-alt/50 rounded-xl p-3">
          <p className="text-xs text-content-muted mb-1">{t('powerZones.vi')}</p>
          <p className="text-lg font-bold text-content">{metrics.variabilityIndex.toFixed(2)}</p>
        </div>
      </div>

      {hasData ? (
        <>
          {/* Stacked bar */}
          <div className="flex h-8 rounded-lg overflow-hidden">
            {zones.map((zone) => {
              const pct = totalTime > 0 ? (zone.durationSeconds / totalTime) * 100 : 0;
              if (pct < 0.5) return null;
              return (
                <div
                  key={zone.name}
                  className="flex items-center justify-center text-xs font-bold text-white/90 transition-all"
                  style={{ width: `${pct}%`, backgroundColor: zone.color, minWidth: pct > 3 ? undefined : '4px' }}
                  title={`${zone.name} - ${formatDurationShort(zone.durationSeconds)}`}
                >
                  {pct > 8 && zone.name}
                </div>
              );
            })}
          </div>

          {/* Zone details */}
          <div className="space-y-2">
            {zones.map((zone) => {
              const pct = totalTime > 0 ? (zone.durationSeconds / totalTime) * 100 : 0;
              const maxLabel = zone.maxValue === Infinity ? '+' : `–${zone.maxValue}`;
              return (
                <div key={zone.name} className="flex items-center gap-3 text-sm">
                  <div className="w-3 h-3 rounded-sm shrink-0" style={{ backgroundColor: zone.color }} />
                  <span className="text-content font-medium w-20 shrink-0">{zone.name}</span>
                  <span className="text-content-muted w-32 shrink-0">{zone.minValue}{maxLabel} {tc('unit.watts')}</span>
                  <div className="flex-1 bg-surface-alt rounded-full h-2">
                    <div
                      className="h-2 rounded-full transition-all"
                      style={{ width: `${Math.max(pct, 1)}%`, backgroundColor: zone.color }}
                    />
                  </div>
                  <span className="text-content-muted w-16 text-right shrink-0">{formatDurationShort(zone.durationSeconds)}</span>
                  <span className="text-content-muted w-12 text-right shrink-0">{pct.toFixed(0)}%</span>
                </div>
              );
            })}
          </div>
        </>
      ) : (
        <p className="text-content-muted text-sm">{t('powerZones.noData')}</p>
      )}

      {/* FTP source */}
      <p className="text-xs text-content-muted">
        {t('powerZones.usingFtp', { value: ftp })}
        {' — '}
        <Link to="/settings#athlete-profile" className="text-accent hover:underline">{t('powerZones.configureFtp')}</Link>
      </p>
    </div>
  );
}
