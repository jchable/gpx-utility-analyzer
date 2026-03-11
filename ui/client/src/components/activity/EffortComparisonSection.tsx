import { useTranslation } from 'react-i18next';
import type { EffortStats } from '../../types/activity';
import { DIFFICULTY_STYLES } from '../../constants/difficulty';

function PerformanceBar({ ratio, label }: { ratio: number; label: string }) {
  // ratio: <1 = faster than model, 1 = matching, >1 = slower
  const pct = Math.min(ratio * 50, 100); // scale so 1.0 = 50%, 2.0 = 100%
  const color = ratio < 0.9 ? '#00ff88' : ratio < 1.1 ? '#00d4ff' : ratio < 1.5 ? '#f59e0b' : '#ef4444';

  return (
    <div>
      <div className="flex justify-between text-xs mb-1">
        <span className="text-content-muted">{label}</span>
        <span className="font-mono" style={{ color }}>{ratio.toFixed(2)}x</span>
      </div>
      <div className="h-2 bg-surface-alt rounded-full overflow-hidden">
        <div
          className="h-full rounded-full transition-all duration-700"
          style={{ width: `${pct}%`, backgroundColor: color }}
        />
      </div>
    </div>
  );
}

export default function EffortComparisonSection({ effort }: { effort: EffortStats }) {
  const { t } = useTranslation('activities');
  const { t: tc } = useTranslation();

  const gradeKey = effort.terrain_difficulty.grade.toLowerCase();
  const diffClass = DIFFICULTY_STYLES[gradeKey] || DIFFICULTY_STYLES.moderate;

  return (
    <div className="bg-surface-card rounded-2xl p-6 border border-border space-y-6">
      <h2 className="text-xl font-semibold text-content flex items-center gap-3">
        <svg className="w-6 h-6 text-emerald-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 7h8m0 0v8m0-8l-8 8-4-4-6 6" />
        </svg>
        {t('effort.title')}
      </h2>

      {/* Row 1: Key metrics */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
        <div className="bg-surface-alt/50 rounded-xl p-4">
          <p className="text-xs text-content-muted mb-1">{t('effort.ke')}</p>
          <p className="text-lg font-bold text-emerald-400">{effort.kilometre_effort.toFixed(1)} <span className="text-sm font-normal text-content-muted">KE</span></p>
        </div>
        <div className="bg-surface-alt/50 rounded-xl p-4">
          <p className="text-xs text-content-muted mb-1">{t('effort.itraPoints')}</p>
          <p className="text-lg font-bold text-content">
            {effort.itra_points.toFixed(1)}
            <span className={`ml-2 text-xs font-bold px-2 py-0.5 rounded-full border ${diffClass}`}>
              {effort.itra_category}
            </span>
          </p>
        </div>
        <div className="bg-surface-alt/50 rounded-xl p-4">
          <p className="text-xs text-content-muted mb-1">{t('effort.efd')}</p>
          <p className="text-lg font-bold text-accent">
            {effort.equivalent_flat_distance_km.toFixed(1)} <span className="text-sm font-normal text-content-muted">{tc('unit.km')}</span>
          </p>
        </div>
        <div className="bg-surface-alt/50 rounded-xl p-4">
          <p className="text-xs text-content-muted mb-1">{t('effort.terrainDifficulty')}</p>
          <p className="text-lg font-bold text-content">
            {effort.terrain_difficulty.score.toFixed(1)}<span className="text-sm font-normal text-content-muted">/10</span>
            <span className={`ml-2 text-xs font-bold px-2 py-0.5 rounded-full border ${diffClass}`}>
              {effort.terrain_difficulty.grade}
            </span>
          </p>
        </div>
      </div>

      {/* Row 2: Time estimates */}
      <div>
        <h3 className="text-sm font-medium text-content-muted mb-3">{t('effort.timeEstimates')}</h3>
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
          <div className="bg-surface-alt/50 rounded-xl p-4">
            <p className="text-xs text-content-muted mb-1">Naismith</p>
            <p className="text-lg font-bold text-content">{effort.naismith_time.display}</p>
          </div>
          <div className="bg-surface-alt/50 rounded-xl p-4">
            <p className="text-xs text-content-muted mb-1">Tobler</p>
            <p className="text-lg font-bold text-content">{effort.tobler_time.display}</p>
          </div>
          <div className="bg-surface-alt/50 rounded-xl p-4">
            <p className="text-xs text-content-muted mb-1">Munter (CAS/SAC)</p>
            <p className="text-lg font-bold text-content">{effort.munter_time.display}</p>
          </div>
        </div>
      </div>

      {/* Row 3: Performance ratios */}
      {(effort.performance_ratio_naismith > 0 || effort.performance_ratio_tobler > 0) && (
        <div>
          <h3 className="text-sm font-medium text-content-muted mb-3">{t('effort.performanceRatios')}</h3>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            {effort.performance_ratio_naismith > 0 && (
              <PerformanceBar ratio={effort.performance_ratio_naismith} label={t('effort.vsNaismith')} />
            )}
            {effort.performance_ratio_tobler > 0 && (
              <PerformanceBar ratio={effort.performance_ratio_tobler} label={t('effort.vsTobler')} />
            )}
          </div>
          <p className="text-xs text-content-muted/70 mt-2">{t('effort.ratioHelp')}</p>
        </div>
      )}
    </div>
  );
}
