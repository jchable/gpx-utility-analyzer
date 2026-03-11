import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useDashboard } from '../hooks/useActivities';
import { ACTIVITY_COLORS } from '../types/activity';
import { formatPageDuration, formatDate } from '../utils/format';

export default function Dashboard() {
  const { t } = useTranslation('dashboard');
  const { t: tc } = useTranslation();
  const { i18n } = useTranslation();
  const { data, isLoading, error } = useDashboard();

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-96">
        <div className="animate-spin rounded-full h-12 w-12 border-t-2 border-b-2 border-accent" />
      </div>
    );
  }

  if (error) {
    return (
      <div className="flex items-center justify-center h-96">
        <p className="text-red-400 text-lg">{t('loadError', { message: error.message })}</p>
      </div>
    );
  }

  if (!data) return null;

  const statWidgets = [
    {
      label: t('totalActivities'),
      value: data.totalActivities.toString(),
      icon: (
        <svg className="w-8 h-8 text-accent" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
        </svg>
      ),
      accent: 'text-accent',
    },
    {
      label: t('distanceThisMonth'),
      value: `${data.distanceThisMonthKm.toFixed(1)} ${tc('unit.km')}`,
      icon: (
        <svg className="w-8 h-8 text-green-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 7h8m0 0v8m0-8l-8 8-4-4-6 6" />
        </svg>
      ),
      accent: 'text-green-400',
    },
    {
      label: t('elevationThisMonth'),
      value: `${Math.round(data.elevationGainThisMonthM)} ${tc('unit.m')}`,
      icon: (
        <svg className="w-8 h-8 text-amber-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 10l7-7m0 0l7 7m-7-7v18" />
        </svg>
      ),
      accent: 'text-amber-400',
    },
    {
      label: t('timeThisMonth'),
      value: formatPageDuration(data.movingTimeThisMonthSeconds, tc),
      icon: (
        <svg className="w-8 h-8 text-purple-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
        </svg>
      ),
      accent: 'text-purple-400',
    },
  ];

  const breakdown = data.activityTypeBreakdown;
  const breakdownEntries = Object.entries(breakdown).sort(([, a], [, b]) => b - a);
  const totalBreakdown = breakdownEntries.reduce((sum, [, count]) => sum + count, 0);

  let cumulativePct = 0;
  const conicStops = breakdownEntries.map(([type, count]) => {
    const pct = totalBreakdown > 0 ? (count / totalBreakdown) * 100 : 0;
    const start = cumulativePct;
    cumulativePct += pct;
    const color = ACTIVITY_COLORS[type] || ACTIVITY_COLORS.other;
    return `${color} ${start}% ${cumulativePct}%`;
  });
  const conicGradient = `conic-gradient(${conicStops.join(', ')})`;

  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-3xl font-bold text-content tracking-tight">{t('title')}</h1>
        <p className="text-content-muted mt-1">{t('subtitle')}</p>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-5">
        {statWidgets.map((widget) => (
          <div
            key={widget.label}
            className="bg-surface-card rounded-2xl p-6 border border-border hover:border-content-muted/30 transition-colors"
          >
            <div className="flex items-center justify-between mb-4">
              <span className="text-content-muted text-sm font-medium">{widget.label}</span>
              {widget.icon}
            </div>
            <p className={`text-3xl font-bold ${widget.accent}`}>{widget.value}</p>
          </div>
        ))}
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="bg-surface-card rounded-2xl p-6 border border-border">
          <h2 className="text-lg font-semibold text-content mb-6">{t('activityBreakdown')}</h2>
          <div className="flex flex-col items-center gap-6">
            <div className="relative w-40 h-40 shrink-0">
              <div
                className="w-full h-full rounded-full"
                style={{ background: totalBreakdown > 0 ? conicGradient : '#334155' }}
              />
              <div className="absolute inset-5 bg-surface-card rounded-full flex items-center justify-center">
                <div className="text-center">
                  <p className="text-2xl font-bold text-content">{totalBreakdown}</p>
                  <p className="text-xs text-content-muted">{t('total')}</p>
                </div>
              </div>
            </div>
            <div className="space-y-3 w-full">
              {breakdownEntries.map(([type, count]) => {
                const pct = totalBreakdown > 0 ? ((count / totalBreakdown) * 100).toFixed(0) : '0';
                return (
                  <div key={type} className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      <div
                        className="w-3 h-3 rounded-full shrink-0"
                        style={{ backgroundColor: ACTIVITY_COLORS[type] || ACTIVITY_COLORS.other }}
                      />
                      <span className="text-sm text-content">
                        {tc(`activityType.${type}`, { defaultValue: type })}
                      </span>
                    </div>
                    <div className="flex items-center gap-2">
                      <span className="text-sm font-medium text-content">{count}</span>
                      <span className="text-xs text-content-muted">({pct}%)</span>
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        </div>

        <div className="lg:col-span-2 bg-surface-card rounded-2xl p-6 border border-border">
          <div className="flex items-center justify-between mb-6">
            <h2 className="text-lg font-semibold text-content">{t('recentActivities')}</h2>
            <Link
              to="/activities"
              className="text-sm text-accent hover:text-accent transition-colors"
            >
              {t('viewAll')}
            </Link>
          </div>
          <div className="space-y-2">
            {data.recentActivities.length === 0 && (
              <p className="text-content-muted text-center py-8">
                {t('emptyState')}
              </p>
            )}
            {data.recentActivities.map((activity) => (
              <Link
                key={activity.id}
                to={`/activities/${activity.id}`}
                className="flex items-center justify-between p-3 rounded-xl hover:bg-surface-alt/50 transition-colors group"
              >
                <div className="flex items-center gap-4 min-w-0">
                  <div
                    className="w-10 h-10 rounded-lg flex items-center justify-center text-xs font-bold shrink-0"
                    style={{
                      backgroundColor:
                        (ACTIVITY_COLORS[activity.activityType] || ACTIVITY_COLORS.other) + '22',
                      color: ACTIVITY_COLORS[activity.activityType] || ACTIVITY_COLORS.other,
                    }}
                  >
                    {tc(`activityType.${activity.activityType}`, { defaultValue: activity.activityType })
                      .slice(0, 3)
                      .toUpperCase()}
                  </div>
                  <div className="min-w-0">
                    <p className="text-content font-medium group-hover:text-accent transition-colors truncate">
                      {activity.name}
                    </p>
                    <p className="text-xs text-content-muted">{formatDate(activity.startTime, i18n.language)}</p>
                  </div>
                </div>
                <div className="flex items-center gap-6 text-sm text-content-muted shrink-0 ml-4">
                  <span>{activity.distanceKm.toFixed(1)} {tc('unit.km')}</span>
                  <span className="hidden sm:inline">{activity.elevationGainM} {tc('unit.m')} D+</span>
                  <span className="hidden md:inline">{formatPageDuration(activity.movingTimeSeconds, tc)}</span>
                </div>
              </Link>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
