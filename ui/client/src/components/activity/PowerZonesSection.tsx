import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import type { ComputedZone } from '../../types/activity';

function formatDuration(seconds: number): string {
  if (seconds < 60) return `${Math.round(seconds)}s`;
  const m = Math.floor(seconds / 60);
  const s = Math.round(seconds % 60);
  if (m < 60) return s > 0 ? `${m}m ${s}s` : `${m}m`;
  const h = Math.floor(m / 60);
  const rm = m % 60;
  return rm > 0 ? `${h}h ${rm}m` : `${h}h`;
}

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
    <div className="bg-[#16213e] rounded-2xl p-6 border border-slate-700/50 space-y-5">
      <div className="flex items-center justify-between flex-wrap gap-3">
        <h2 className="text-xl font-semibold text-white flex items-center gap-3">
          <svg className="w-6 h-6 text-yellow-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 10V3L4 14h7v7l9-11h-7z" />
          </svg>
          {t('powerZones.title')}
        </h2>
      </div>

      {/* Advanced metrics */}
      <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3">
        <div className="bg-slate-800/50 rounded-xl p-3">
          <p className="text-xs text-slate-500 mb-1">{t('powerZones.np')}</p>
          <p className="text-lg font-bold text-yellow-400">{metrics.normalizedPower} {tc('unit.watts')}</p>
        </div>
        <div className="bg-slate-800/50 rounded-xl p-3">
          <p className="text-xs text-slate-500 mb-1">{t('powerZones.avgPower')}</p>
          <p className="text-lg font-bold text-white">{avgWatts} {tc('unit.watts')}</p>
        </div>
        <div className="bg-slate-800/50 rounded-xl p-3">
          <p className="text-xs text-slate-500 mb-1">{t('powerZones.maxPower')}</p>
          <p className="text-lg font-bold text-white">{maxWatts} {tc('unit.watts')}</p>
        </div>
        <div className="bg-slate-800/50 rounded-xl p-3">
          <p className="text-xs text-slate-500 mb-1">{t('powerZones.if')}</p>
          <p className="text-lg font-bold text-white">{metrics.intensityFactor.toFixed(2)}</p>
        </div>
        <div className="bg-slate-800/50 rounded-xl p-3">
          <p className="text-xs text-slate-500 mb-1">{t('powerZones.tss')}</p>
          <p className="text-lg font-bold text-white">{metrics.tss}</p>
        </div>
        <div className="bg-slate-800/50 rounded-xl p-3">
          <p className="text-xs text-slate-500 mb-1">{t('powerZones.vi')}</p>
          <p className="text-lg font-bold text-white">{metrics.variabilityIndex.toFixed(2)}</p>
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
                  title={`${zone.name} - ${formatDuration(zone.durationSeconds)}`}
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
                  <span className="text-slate-300 font-medium w-20 shrink-0">{zone.name}</span>
                  <span className="text-slate-500 w-32 shrink-0">{zone.minValue}{maxLabel} {tc('unit.watts')}</span>
                  <div className="flex-1 bg-slate-800 rounded-full h-2">
                    <div
                      className="h-2 rounded-full transition-all"
                      style={{ width: `${Math.max(pct, 1)}%`, backgroundColor: zone.color }}
                    />
                  </div>
                  <span className="text-slate-400 w-16 text-right shrink-0">{formatDuration(zone.durationSeconds)}</span>
                  <span className="text-slate-500 w-12 text-right shrink-0">{pct.toFixed(0)}%</span>
                </div>
              );
            })}
          </div>
        </>
      ) : (
        <p className="text-slate-500 text-sm">{t('powerZones.noData')}</p>
      )}

      {/* FTP source */}
      <p className="text-xs text-slate-500">
        {t('powerZones.usingFtp', { value: ftp })}
        {' — '}
        <Link to="/settings" className="text-cyan-400 hover:underline">{t('powerZones.configureFtp')}</Link>
      </p>
    </div>
  );
}
