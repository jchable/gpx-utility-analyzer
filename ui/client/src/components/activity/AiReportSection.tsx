import { useTranslation } from 'react-i18next';
import type { TrackReport } from '../../types/activity';
import { DIFFICULTY_STYLES } from '../../constants/difficulty';

export default function AiReportSection({ report }: { report: TrackReport }) {
  const { t } = useTranslation('activities');
  const { t: tc } = useTranslation();

  const difficultyGrade = report.difficulty.grade.toLowerCase();
  const difficultyClass = DIFFICULTY_STYLES[difficultyGrade] || DIFFICULTY_STYLES.moderate;

  return (
    <div className="bg-surface-card rounded-2xl p-6 border border-border space-y-6">
      <div className="flex items-center justify-between">
        <h2 className="text-xl font-semibold text-content flex items-center gap-3">
          <svg className="w-6 h-6 text-purple-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z" />
          </svg>
          {t('aiReport.title')}
        </h2>
        <span className={`text-sm font-bold px-3 py-1 rounded-full border ${difficultyClass}`}>
          {report.difficulty.grade} ({report.difficulty.score}/10)
        </span>
      </div>

      {/* Summary */}
      <div>
        <h3 className="text-sm font-medium text-content-muted mb-2">{t('aiReport.summary')}</h3>
        <p className="text-content leading-relaxed">{report.summary}</p>
      </div>

      {/* Difficulty Justification */}
      <div>
        <h3 className="text-sm font-medium text-content-muted mb-2">{t('aiReport.difficultyAssessment')}</h3>
        <p className="text-content text-sm">{report.difficulty.justification}</p>
      </div>

      {/* Effort */}
      {report.effort && (
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
          <div className="bg-surface-alt/50 rounded-xl p-4">
            <p className="text-xs text-content-muted mb-1">{t('aiReport.fitnessLevel')}</p>
            <p className="text-sm font-semibold text-content">{report.effort.fitness_level}</p>
          </div>
          <div className="bg-surface-alt/50 rounded-xl p-4">
            <p className="text-xs text-content-muted mb-1">{t('aiReport.estimatedDuration')}</p>
            <p className="text-sm font-semibold text-content">{report.effort.estimated_duration}</p>
          </div>
          {report.effort.calorie_estimate && (
            <div className="bg-surface-alt/50 rounded-xl p-4">
              <p className="text-xs text-content-muted mb-1">{t('aiReport.calories')}</p>
              <p className="text-sm font-semibold text-content">
                ~{report.effort.calorie_estimate} {tc('unit.kcal')}
              </p>
            </div>
          )}
        </div>
      )}

      {/* Key Segments */}
      {report.key_segments && report.key_segments.length > 0 && (
        <div>
          <h3 className="text-sm font-medium text-content-muted mb-3">{t('aiReport.keySegments')}</h3>
          <div className="space-y-2">
            {report.key_segments.map((seg, i) => (
              <div
                key={i}
                className="flex items-start gap-3 bg-surface-alt/30 rounded-xl p-3"
              >
                <span className="text-xs font-bold text-accent bg-cyan-400/10 px-2 py-1 rounded shrink-0 uppercase">
                  {seg.type}
                </span>
                <div className="min-w-0 flex-1">
                  <p className="text-sm text-content">{seg.description}</p>
                  <div className="flex gap-4 mt-1">
                    {seg.distance_km != null && (
                      <span className="text-xs text-content-muted">{seg.distance_km} {tc('unit.km')}</span>
                    )}
                    {seg.elevation_change != null && (
                      <span className="text-xs text-content-muted">
                        {seg.elevation_change > 0 ? '+' : ''}
                        {seg.elevation_change} {tc('unit.m')}
                      </span>
                    )}
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Recommendations */}
      {report.recommendations && report.recommendations.length > 0 && (
        <div>
          <h3 className="text-sm font-medium text-content-muted mb-3">{t('aiReport.recommendations')}</h3>
          <ul className="space-y-2">
            {report.recommendations.map((rec, i) => (
              <li key={i} className="flex items-start gap-2 text-sm text-content">
                <svg className="w-4 h-4 text-green-400 mt-0.5 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                </svg>
                {rec}
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
