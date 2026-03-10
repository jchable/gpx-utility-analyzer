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

interface Props {
  zones: ComputedZone[];
  trimp: number;
  maxHR: number;
  source: 'user' | 'age' | 'observed';
  avgBpm?: number;
  maxBpm?: number;
}

export default function HRZonesSection({ zones, trimp, maxHR, source, avgBpm, maxBpm }: Props) {
  const { t } = useTranslation('activities');
  const { t: tc } = useTranslation();

  const totalTime = zones.reduce((sum, z) => sum + z.durationSeconds, 0);
  const hasData = totalTime > 0;

  return (
    <div className="bg-[#16213e] rounded-2xl p-6 border border-slate-700/50 space-y-5">
      <div className="flex items-center justify-between flex-wrap gap-3">
        <h2 className="text-xl font-semibold text-white flex items-center gap-3">
          <svg className="w-6 h-6 text-red-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4.318 6.318a4.5 4.5 0 000 6.364L12 20.364l7.682-7.682a4.5 4.5 0 00-6.364-6.364L12 7.636l-1.318-1.318a4.5 4.5 0 00-6.364 0z" />
          </svg>
          {t('hrZones.title')}
        </h2>
        {hasData && (
          <div className="relative group">
            <span className="text-sm font-bold px-3 py-1 rounded-full bg-orange-500/20 text-orange-400 border border-orange-500/30 cursor-help">
              TRIMP {trimp}
            </span>
            <div className="absolute right-0 top-full mt-2 w-64 p-3 bg-slate-900 border border-slate-700 rounded-lg shadow-xl opacity-0 invisible group-hover:opacity-100 group-hover:visible transition-all z-50">
              <p className="text-xs text-slate-300 mb-2">{t('hrZones.trimpTooltipDesc')}</p>
              <div className="space-y-0.5 text-xs text-slate-400">
                <p>{t('hrZones.trimpScaleLight')}</p>
                <p>{t('hrZones.trimpScaleModerate')}</p>
                <p>{t('hrZones.trimpScaleHigh')}</p>
                <p>{t('hrZones.trimpScaleVeryHigh')}</p>
              </div>
            </div>
          </div>
        )}
      </div>

      {/* HR summary stats */}
      {(avgBpm || maxBpm) && (
        <div className="grid grid-cols-2 gap-4">
          {avgBpm != null && (
            <div className="bg-slate-800/50 rounded-xl p-4">
              <p className="text-xs text-slate-500 mb-1">{t('detail.avgHR')}</p>
              <p className="text-lg font-bold text-red-400">{Math.round(avgBpm)} {tc('unit.bpm')}</p>
            </div>
          )}
          {maxBpm != null && (
            <div className="bg-slate-800/50 rounded-xl p-4">
              <p className="text-xs text-slate-500 mb-1">{t('detail.maxHR')}</p>
              <p className="text-lg font-bold text-red-400">{maxBpm} {tc('unit.bpm')}</p>
            </div>
          )}
        </div>
      )}

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
              return (
                <div key={zone.name} className="flex items-center gap-3 text-sm">
                  <div className="w-3 h-3 rounded-sm shrink-0" style={{ backgroundColor: zone.color }} />
                  <span className="text-slate-300 font-medium w-20 shrink-0">{zone.name}</span>
                  <span className="text-slate-500 w-32 shrink-0">{zone.minValue}–{zone.maxValue} {tc('unit.bpm')}</span>
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
        <p className="text-slate-500 text-sm">{t('hrZones.noData')}</p>
      )}

      {/* Source indicator */}
      <p className="text-xs text-slate-500">
        {source === 'user'
          ? t('hrZones.usingUserMaxHR', { value: maxHR })
          : source === 'age'
            ? t('hrZones.usingAgeMaxHR', { value: maxHR })
            : t('hrZones.usingObservedMaxHR', { value: maxHR })}
        {source !== 'user' && (
          <>
            {' — '}
            <Link to="/settings#athlete-profile" className="text-cyan-400 hover:underline">{t('hrZones.configureMaxHR')}</Link>
          </>
        )}
      </p>
    </div>
  );
}
