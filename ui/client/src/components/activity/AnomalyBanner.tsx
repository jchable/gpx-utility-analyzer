import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { AnomalyItem, AnomalyReport } from '../../types/activity';

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

function formatImpact(meters: number): string {
  if (Math.abs(meters) >= 1000) return `${(meters / 1000).toFixed(1)} km`;
  return `${Math.round(meters)} m`;
}

function formatTime(seconds: number): string {
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

interface WarningGroup {
  type: string;
  items: AnomalyItem[];
  totalDistanceImpact: number;
}

function groupWarnings(anomalies: AnomalyItem[]): WarningGroup[] {
  const map = new Map<string, WarningGroup>();
  for (const a of anomalies) {
    if (!map.has(a.type)) map.set(a.type, { type: a.type, items: [], totalDistanceImpact: 0 });
    const g = map.get(a.type)!;
    g.items.push(a);
    g.totalDistanceImpact += a.distance_impact_m;
  }
  return [...map.values()].sort((a, b) => Math.abs(b.totalDistanceImpact) - Math.abs(a.totalDistanceImpact));
}

interface AnomalyBannerProps {
  report: AnomalyReport;
  activityStatus?: string;
  onFixAnomalies?: () => void;
}

export default function AnomalyBanner({ report, activityStatus, onFixAnomalies }: AnomalyBannerProps) {
  const { t } = useTranslation('activities');
  const [expanded, setExpanded] = useState(false);
  const [expandedGroups, setExpandedGroups] = useState<Set<string>>(new Set());

  const isFixing = activityStatus === 'Pending' || activityStatus === 'Analyzing' || activityStatus === 'AiProcessing';

  // ── Corrected state: replace entire banner with a simple tag ───────────────
  if (report.correction_applied) {
    return (
      <div className="flex items-center gap-2 px-4 py-2.5 rounded-xl bg-green-500/15 border border-green-500/30 w-fit">
        <svg className="w-4 h-4 text-green-400 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
        </svg>
        <span className="text-sm font-medium text-green-400">{t('anomaly.trackCorrected')}</span>
        {Math.abs(report.distance_impact_m) >= 10 && (
          <span className="text-xs text-green-300/70">
            {report.distance_impact_m < 0
              ? `+${formatImpact(Math.abs(report.distance_impact_m))} ${t('anomaly.recovered')}`
              : null}
          </span>
        )}
      </div>
    );
  }

  const anomalies = report.anomalies ?? [];
  const criticals = anomalies.filter(a => a.severity === 'critical');
  const warnings = anomalies.filter(a => a.severity === 'warning');
  const infos = anomalies.filter(a => a.severity === 'info');
  const warningGroups = groupWarnings(warnings);
  const infoGroups = groupWarnings(infos);

  const scoreColor = getScoreColor(report.quality_score);
  const scoreBg = getScoreBg(report.quality_score);

  const toggleGroup = (type: string) => {
    setExpandedGroups(prev => {
      const next = new Set(prev);
      next.has(type) ? next.delete(type) : next.add(type);
      return next;
    });
  };

  return (
    <div className={`rounded-2xl border p-4 ${scoreBg}`}>
      {/* Header */}
      <button onClick={() => setExpanded(!expanded)} className="w-full flex items-center justify-between gap-3">
        <div className="flex items-center gap-3 min-w-0 flex-wrap">
          <svg className={`w-5 h-5 ${scoreColor} shrink-0`} fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4.5c-.77-.833-2.694-.833-3.464 0L3.34 16.5c-.77.833.192 2.5 1.732 2.5z" />
          </svg>
          <span className={`font-semibold ${scoreColor}`}>
            {t('anomaly.qualityScore')}: {report.quality_score}/100
          </span>
          <span className="text-sm text-content-muted flex gap-1.5 flex-wrap">
            {report.critical_count > 0 && (
              <span className="text-red-400">{report.critical_count} {t('anomaly.critical')}</span>
            )}
            {report.critical_count > 0 && report.warning_count > 0 && <span>·</span>}
            {report.warning_count > 0 && (
              <span className="text-amber-400">{report.warning_count} {t('anomaly.warning')}</span>
            )}
            {(report.critical_count > 0 || report.warning_count > 0) && infos.length > 0 && <span>·</span>}
            {infos.length > 0 && (
              <span className="text-blue-400">{infos.length} {t('anomaly.info')}</span>
            )}
          </span>
        </div>
        <svg className={`w-5 h-5 text-content-muted transition-transform shrink-0 ${expanded ? 'rotate-180' : ''}`}
          fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
        </svg>
      </button>

      {/* Impact summary */}
      {(Math.abs(report.distance_impact_m) >= 1 || Math.abs(report.time_impact_s) >= 1) && (
        <div className="flex gap-4 mt-2 ml-8 text-sm text-content-muted">
          {Math.abs(report.distance_impact_m) >= 1 && (
            <span>{t('anomaly.distanceImpact')}: {formatImpact(report.distance_impact_m)}</span>
          )}
          {Math.abs(report.time_impact_s) >= 1 && (
            <span>{t('anomaly.timeImpact')}: {formatTime(report.time_impact_s)}</span>
          )}
        </div>
      )}

      {/* Fix button */}
      {onFixAnomalies && report.critical_count > 0 && (
        <div className="mt-3 ml-8">
          {isFixing ? (
            <span className="flex items-center gap-1.5 text-xs text-content-muted">
              <div className="w-3.5 h-3.5 rounded-full border-2 border-content-muted border-t-transparent animate-spin" />
              {t('anomaly.fixing')}
            </span>
          ) : (
            <button
              onClick={(e) => { e.stopPropagation(); onFixAnomalies(); }}
              className="px-3 py-1.5 rounded-lg bg-orange-600/80 hover:bg-orange-500 text-white text-xs font-medium transition-colors flex items-center gap-1.5"
            >
              <svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M11 4a2 2 0 114 0v1a1 1 0 001 1h3a1 1 0 011 1v3a1 1 0 01-1 1h-1a2 2 0 100 4h1a1 1 0 011 1v3a1 1 0 01-1 1h-3a1 1 0 01-1-1v-1a2 2 0 10-4 0v1a1 1 0 01-1 1H7a1 1 0 01-1-1v-3a1 1 0 00-1-1H4a2 2 0 110-4h1a1 1 0 001-1V7a1 1 0 011-1h3a1 1 0 001-1V4z" />
              </svg>
              {t('anomaly.fixButton')}
            </button>
          )}
        </div>
      )}

      {/* Expanded detail */}
      {expanded && (
        <div className="mt-4 space-y-4 ml-8">

          {/* Critical — grouped by type */}
          {criticals.length > 0 && (() => {
            const criticalGroups = groupWarnings(criticals);
            return (
              <div>
                <p className="text-xs font-semibold text-red-400 uppercase tracking-wide mb-2">
                  {t('anomaly.critical')} ({criticals.length})
                </p>
                <div className="space-y-1.5">
                  {criticalGroups.map((group) => (
                    <div key={group.type} className="bg-red-500/10 rounded-xl overflow-hidden">
                      <button
                        onClick={() => toggleGroup(`crit_${group.type}`)}
                        className="w-full flex items-center justify-between gap-2 px-3 py-2 text-left"
                      >
                        <div className="flex items-center gap-2 min-w-0">
                          <span className="text-sm font-medium text-content">{group.type.replace(/_/g, ' ')}</span>
                          <span className="text-xs text-red-400/70">×{group.items.length}</span>
                          {Math.abs(group.totalDistanceImpact) >= 1 && (
                            <span className="text-xs text-content-muted">
                              — {formatImpact(group.totalDistanceImpact)}
                            </span>
                          )}
                        </div>
                        <svg className={`w-4 h-4 text-content-muted shrink-0 transition-transform ${expandedGroups.has(`crit_${group.type}`) ? 'rotate-180' : ''}`}
                          fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
                        </svg>
                      </button>
                      {expandedGroups.has(`crit_${group.type}`) && (
                        <div className="border-t border-red-500/20 divide-y divide-red-500/10">
                          {group.items.map((a, i) => {
                            const timeRange = a.start_time
                              ? `${new Date(a.start_time).toLocaleTimeString()} – ${new Date(a.end_time!).toLocaleTimeString()}`
                              : `pts ${a.start_index}–${a.end_index}`;
                            return (
                              <div key={i} className="px-3 py-2">
                                <p className="text-xs text-content-muted">
                                  {timeRange}
                                  {Math.abs(a.distance_impact_m) >= 1 && (
                                    <span className="ml-2 text-red-300/80">{formatImpact(a.distance_impact_m)}</span>
                                  )}
                                </p>
                                {a.description && (
                                  <p className="text-xs text-content-muted/60 mt-0.5">{a.description}</p>
                                )}
                              </div>
                            );
                          })}
                        </div>
                      )}
                    </div>
                  ))}
                </div>
              </div>
            );
          })()}

          {/* Warnings — grouped by type */}
          {warningGroups.length > 0 && (
            <div>
              <p className="text-xs font-semibold text-amber-400 uppercase tracking-wide mb-2">
                {t('anomaly.warning')} ({warnings.length})
              </p>
              <div className="space-y-1.5">
                {warningGroups.map((group) => (
                  <div key={group.type} className="bg-amber-500/10 rounded-xl overflow-hidden">
                    <button
                      onClick={() => toggleGroup(group.type)}
                      className="w-full flex items-center justify-between gap-2 px-3 py-2 text-left"
                    >
                      <div className="flex items-center gap-2 min-w-0">
                        <span className="text-sm font-medium text-content">{group.type.replace(/_/g, ' ')}</span>
                        <span className="text-xs text-amber-400/70">×{group.items.length}</span>
                        {Math.abs(group.totalDistanceImpact) >= 1 && (
                          <span className="text-xs text-content-muted">
                            — {formatImpact(group.totalDistanceImpact)}
                          </span>
                        )}
                      </div>
                      <svg className={`w-4 h-4 text-content-muted shrink-0 transition-transform ${expandedGroups.has(group.type) ? 'rotate-180' : ''}`}
                        fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
                      </svg>
                    </button>
                    {expandedGroups.has(group.type) && (
                      <div className="border-t border-amber-500/20 divide-y divide-amber-500/10">
                        {group.items.map((a, i) => {
                          const timeRange = a.start_time
                            ? `${new Date(a.start_time).toLocaleTimeString()} – ${new Date(a.end_time!).toLocaleTimeString()}`
                            : `pts ${a.start_index}–${a.end_index}`;
                          return (
                            <div key={i} className="px-3 py-2">
                              <p className="text-xs text-content-muted">
                                {timeRange}
                                {Math.abs(a.distance_impact_m) >= 1 && (
                                  <span className="ml-2">{formatImpact(a.distance_impact_m)}</span>
                                )}
                              </p>
                            </div>
                          );
                        })}
                      </div>
                    )}
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Info — grouped by type */}
          {infoGroups.length > 0 && (
            <div>
              <p className="text-xs font-semibold text-blue-400 uppercase tracking-wide mb-2">
                {t('anomaly.info')} ({infos.length})
              </p>
              <div className="space-y-1.5">
                {infoGroups.map((group) => (
                  <div key={group.type} className="bg-blue-500/10 rounded-xl overflow-hidden">
                    <button
                      onClick={() => toggleGroup(`info_${group.type}`)}
                      className="w-full flex items-center justify-between gap-2 px-3 py-2 text-left"
                    >
                      <div className="flex items-center gap-2 min-w-0">
                        <span className="text-sm font-medium text-content">{group.type.replace(/_/g, ' ')}</span>
                        <span className="text-xs text-blue-400/70">×{group.items.length}</span>
                        {Math.abs(group.totalDistanceImpact) >= 1 && (
                          <span className="text-xs text-content-muted">
                            — {formatImpact(group.totalDistanceImpact)}
                          </span>
                        )}
                      </div>
                      <svg className={`w-4 h-4 text-content-muted shrink-0 transition-transform ${expandedGroups.has(`info_${group.type}`) ? 'rotate-180' : ''}`}
                        fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
                      </svg>
                    </button>
                    {expandedGroups.has(`info_${group.type}`) && (
                      <div className="border-t border-blue-500/20 divide-y divide-blue-500/10">
                        {group.items.map((a, i) => {
                          const timeRange = a.start_time
                            ? `${new Date(a.start_time).toLocaleTimeString()} – ${new Date(a.end_time!).toLocaleTimeString()}`
                            : `pts ${a.start_index}–${a.end_index}`;
                          return (
                            <div key={i} className="px-3 py-2">
                              <p className="text-xs text-content-muted">
                                {timeRange}
                                {Math.abs(a.distance_impact_m) >= 1 && (
                                  <span className="ml-2">{formatImpact(a.distance_impact_m)}</span>
                                )}
                              </p>
                              {a.description && (
                                <p className="text-xs text-content-muted/60 mt-0.5">{a.description}</p>
                              )}
                            </div>
                          );
                        })}
                      </div>
                    )}
                  </div>
                ))}
              </div>
            </div>
          )}

        </div>
      )}
    </div>
  );
}
