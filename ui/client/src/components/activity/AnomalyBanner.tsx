import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { AnomalyReport } from '../../types/activity';

const SEVERITY_ICONS: Record<string, { color: string; bg: string }> = {
  critical: { color: 'text-red-400', bg: 'bg-red-500/20' },
  warning: { color: 'text-amber-400', bg: 'bg-amber-500/20' },
  info: { color: 'text-blue-400', bg: 'bg-blue-500/20' },
};

function getScoreColor(score: number): string {
  if (score >= 90) return 'text-green-400';
  if (score >= 70) return 'text-amber-400';
  if (score >= 50) return 'text-orange-400';
  return 'text-red-400';
}

function getScoreBg(score: number): string {
  if (score >= 90) return 'bg-green-500/20 border-green-500/30';
  if (score >= 70) return 'bg-amber-500/20 border-amber-500/30';
  if (score >= 50) return 'bg-orange-500/20 border-orange-500/30';
  return 'bg-red-500/20 border-red-500/30';
}

function formatImpactDistance(meters: number): string {
  if (Math.abs(meters) >= 1000) return `${(meters / 1000).toFixed(1)} km`;
  return `${Math.round(meters)} m`;
}

function formatImpactTime(seconds: number): string {
  const abs = Math.abs(seconds);
  if (abs >= 3600) {
    const h = Math.floor(abs / 3600);
    const m = Math.floor((abs % 3600) / 60);
    return `${h}h${m.toString().padStart(2, '0')}`;
  }
  if (abs >= 60) {
    const m = Math.floor(abs / 60);
    const s = Math.floor(abs % 60);
    return `${m}m${s.toString().padStart(2, '0')}s`;
  }
  return `${Math.round(abs)}s`;
}

export default function AnomalyBanner({ report, onFixAnomalies }: { report: AnomalyReport; onFixAnomalies?: () => void }) {
  const { t } = useTranslation('activities');
  const [expanded, setExpanded] = useState(false);

  const scoreColor = getScoreColor(report.quality_score);
  const scoreBg = getScoreBg(report.quality_score);

  return (
    <div className={`rounded-2xl border p-4 ${scoreBg}`}>
      {/* Header row */}
      <button
        onClick={() => setExpanded(!expanded)}
        className="w-full flex items-center justify-between gap-3"
      >
        <div className="flex items-center gap-3 min-w-0">
          <svg className={`w-5 h-5 ${scoreColor} shrink-0`} fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4.5c-.77-.833-2.694-.833-3.464 0L3.34 16.5c-.77.833.192 2.5 1.732 2.5z" />
          </svg>
          <span className={`font-semibold ${scoreColor}`}>
            {t('anomaly.qualityScore')}: {report.quality_score}/100
          </span>
          <span className="text-sm text-content-muted">
            {report.critical_count > 0 && (
              <span className="text-red-400">{report.critical_count} {t('anomaly.critical')}</span>
            )}
            {report.critical_count > 0 && report.warning_count > 0 && ', '}
            {report.warning_count > 0 && (
              <span className="text-amber-400">{report.warning_count} {t('anomaly.warning')}</span>
            )}
            {(report.critical_count > 0 || report.warning_count > 0) && report.info_count > 0 && ', '}
            {report.info_count > 0 && (
              <span className="text-blue-400">{report.info_count} {t('anomaly.info')}</span>
            )}
          </span>
        </div>
        <svg
          className={`w-5 h-5 text-content-muted transition-transform shrink-0 ${expanded ? 'rotate-180' : ''}`}
          fill="none" stroke="currentColor" viewBox="0 0 24 24"
        >
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
        </svg>
      </button>

      {/* Impact summary */}
      {(Math.abs(report.distance_impact_m) >= 1 || Math.abs(report.time_impact_s) >= 1) && (
        <div className="flex gap-4 mt-2 ml-8 text-sm text-content-muted">
          {Math.abs(report.distance_impact_m) >= 1 && (
            <span>{t('anomaly.distanceImpact')}: {formatImpactDistance(report.distance_impact_m)}</span>
          )}
          {Math.abs(report.time_impact_s) >= 1 && (
            <span>{t('anomaly.timeImpact')}: {formatImpactTime(report.time_impact_s)}</span>
          )}
          {report.correction_applied && (
            <span className="text-green-400">{t('anomaly.corrected')}</span>
          )}
        </div>
      )}

      {/* Fix button — shown when critical anomalies exist and not yet corrected */}
      {onFixAnomalies && report.critical_count > 0 && !report.correction_applied && (
        <div className="mt-3 ml-8">
          <button
            onClick={(e) => { e.stopPropagation(); onFixAnomalies(); }}
            className="px-3 py-1.5 rounded-lg bg-orange-600/80 hover:bg-orange-500 text-white text-xs font-medium transition-colors flex items-center gap-1.5"
          >
            <svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M11 4a2 2 0 114 0v1a1 1 0 001 1h3a1 1 0 011 1v3a1 1 0 01-1 1h-1a2 2 0 100 4h1a1 1 0 011 1v3a1 1 0 01-1 1h-3a1 1 0 01-1-1v-1a2 2 0 10-4 0v1a1 1 0 01-1 1H7a1 1 0 01-1-1v-3a1 1 0 00-1-1H4a2 2 0 110-4h1a1 1 0 001-1V7a1 1 0 011-1h3a1 1 0 001-1V4z" />
            </svg>
            {t('anomaly.fixButton')}
          </button>
        </div>
      )}

      {/* Expandable anomaly list */}
      {expanded && report.anomalies && report.anomalies.length > 0 && (
        <div className="mt-4 space-y-2 ml-8">
          {report.anomalies.map((a, i) => {
            const sev = SEVERITY_ICONS[a.severity] || SEVERITY_ICONS.info;
            const timeRange = a.start_time
              ? `${new Date(a.start_time).toLocaleTimeString()} - ${new Date(a.end_time!).toLocaleTimeString()}`
              : `points ${a.start_index}-${a.end_index}`;

            return (
              <div key={i} className="flex items-start gap-2 bg-surface-alt/50 rounded-xl p-3">
                <span className={`text-xs font-bold px-2 py-0.5 rounded uppercase shrink-0 ${sev.color} ${sev.bg}`}>
                  {a.severity}
                </span>
                <div className="min-w-0 flex-1">
                  <p className="text-sm text-content">
                    <span className="font-medium text-content">{a.type.replace(/_/g, ' ')}</span>
                    <span className="text-content-muted ml-2">({timeRange})</span>
                  </p>
                  <p className="text-xs text-content-muted mt-0.5">{a.description}</p>
                  <div className="flex gap-3 mt-1">
                    {Math.abs(a.distance_impact_m) >= 1 && (
                      <span className="text-xs text-content-muted">
                        {t('anomaly.distanceImpact')}: {formatImpactDistance(a.distance_impact_m)}
                      </span>
                    )}
                    {a.was_corrected && (
                      <span className="text-xs text-green-400">{t('anomaly.corrected')}</span>
                    )}
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
