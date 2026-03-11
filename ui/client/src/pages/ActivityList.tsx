import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useActivities } from '../hooks/useActivities';
import { ACTIVITY_COLORS, ACTIVITY_TYPES } from '../types/activity';
import { formatPageDuration, formatDate } from '../utils/format';

export default function ActivityList() {
  const { t } = useTranslation('activities');
  const { t: tc } = useTranslation();
  const { i18n } = useTranslation();
  const [page, setPage] = useState(1);
  const [typeFilter, setTypeFilter] = useState('');
  const { data: activities, isLoading, error } = useActivities(page, typeFilter || undefined);

  return (
    <div className="space-y-6">
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold text-content tracking-tight">{t('title')}</h1>
          <p className="text-content-muted mt-1">{t('subtitle')}</p>
        </div>
        <div className="flex items-center gap-3">
          <select
            value={typeFilter}
            onChange={(e) => {
              setTypeFilter(e.target.value);
              setPage(1);
            }}
            className="bg-surface-card text-content border border-border rounded-lg px-4 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500 focus:border-transparent appearance-none cursor-pointer"
          >
            <option value="">{t('allTypes')}</option>
            {ACTIVITY_TYPES.map((type) => (
              <option key={type} value={type}>
                {tc(`activityType.${type}`)}
              </option>
            ))}
          </select>
          <Link
            to="/upload"
            className="bg-cyan-600 hover:bg-cyan-500 text-white px-4 py-2.5 rounded-lg text-sm font-medium transition-colors flex items-center gap-2"
          >
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
            </svg>
            {tc('button.upload')}
          </Link>
        </div>
      </div>

      {isLoading && (
        <div className="flex items-center justify-center h-64">
          <div className="animate-spin rounded-full h-10 w-10 border-t-2 border-b-2 border-accent" />
        </div>
      )}

      {error && (
        <div className="bg-red-900/20 border border-red-800 rounded-xl p-4">
          <p className="text-red-400">{t('loadError', { message: error.message })}</p>
        </div>
      )}

      {activities && activities.length === 0 && (
        <div className="text-center py-16">
          <svg className="w-16 h-16 mx-auto text-content-muted/70 mb-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M9 20l-5.447-2.724A1 1 0 013 16.382V5.618a1 1 0 011.447-.894L9 7m0 13l6-3m-6 3V7m6 10l4.553 2.276A1 1 0 0021 18.382V7.618a1 1 0 00-.553-.894L15 4m0 13V4m0 0L9 7" />
          </svg>
          <p className="text-content-muted text-lg">{t('emptyState')}</p>
          <p className="text-content-muted text-sm mt-1">{t('emptyStateHint')}</p>
        </div>
      )}

      {activities && activities.length > 0 && (
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
          {activities.map((activity) => {
            const color = ACTIVITY_COLORS[activity.activityType] || ACTIVITY_COLORS.other;
            const label = tc(`activityType.${activity.activityType}`, { defaultValue: activity.activityType });

            return (
              <Link
                key={activity.id}
                to={`/activities/${activity.id}`}
                className="bg-surface-card rounded-2xl p-5 border border-border hover:border-content-muted/30 transition-all hover:shadow-lg hover:shadow-black/20 group"
              >
                <div className="flex items-start justify-between mb-4">
                  <div className="min-w-0 flex-1">
                    <h3 className="text-content font-semibold truncate group-hover:text-accent transition-colors">
                      {activity.name}
                    </h3>
                    <p className="text-xs text-content-muted mt-1">{formatDate(activity.startTime, i18n.language, { weekday: 'short', month: 'short', day: 'numeric', year: 'numeric' })}</p>
                  </div>
                  <span
                    className="text-xs font-bold px-2.5 py-1 rounded-full shrink-0 ml-3"
                    style={{ backgroundColor: color + '22', color }}
                  >
                    {label}
                  </span>
                </div>

                <div className="grid grid-cols-3 gap-3">
                  <div>
                    <p className="text-xs text-content-muted mb-0.5">{t('distance')}</p>
                    <p className="text-sm font-semibold text-content">{activity.distanceKm.toFixed(1)} {tc('unit.km')}</p>
                  </div>
                  <div>
                    <p className="text-xs text-content-muted mb-0.5">{t('elevationGain')}</p>
                    <p className="text-sm font-semibold text-content">{Math.round(activity.elevationGainM)} {tc('unit.m')}</p>
                  </div>
                  <div>
                    <p className="text-xs text-content-muted mb-0.5">{t('time')}</p>
                    <p className="text-sm font-semibold text-content">{formatPageDuration(activity.movingTimeSeconds, tc)}</p>
                  </div>
                </div>

                <div className="mt-4 flex items-center gap-2">
                  <div
                    className={`w-2 h-2 rounded-full ${
                      activity.status === 'Completed'
                        ? 'bg-green-400'
                        : activity.status === 'Failed'
                          ? 'bg-red-400'
                          : 'bg-amber-400 animate-pulse'
                    }`}
                  />
                  <span className="text-xs text-content-muted">{tc(`status.${activity.status}`)}</span>
                  {activity.source !== 'upload' && (
                    <span className="text-xs text-content-muted/70 ml-auto">
                      {t('via', { source: activity.source })}
                    </span>
                  )}
                </div>
              </Link>
            );
          })}
        </div>
      )}

      {activities && activities.length > 0 && (
        <div className="flex items-center justify-center gap-3 pt-4">
          <button
            onClick={() => setPage((p) => Math.max(1, p - 1))}
            disabled={page === 1}
            className="px-4 py-2 rounded-lg bg-surface-card border border-border text-content text-sm disabled:opacity-40 disabled:cursor-not-allowed hover:bg-surface-alt/50 transition-colors"
          >
            {tc('button.previous')}
          </button>
          <span className="text-content-muted text-sm">{tc('format.page', { page })}</span>
          <button
            onClick={() => setPage((p) => p + 1)}
            disabled={activities.length < 20}
            className="px-4 py-2 rounded-lg bg-surface-card border border-border text-content text-sm disabled:opacity-40 disabled:cursor-not-allowed hover:bg-surface-alt/50 transition-colors"
          >
            {tc('button.next')}
          </button>
        </div>
      )}
    </div>
  );
}
