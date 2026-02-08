import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useActivities } from '../hooks/useActivities';
import { ACTIVITY_COLORS, ACTIVITY_LABELS } from '../types/activity';

function formatDuration(seconds: number): string {
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  if (h > 0) return `${h}h ${m}m`;
  return `${m}m`;
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('en-US', {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  });
}

const activityTypes = [
  { value: '', label: 'All Types' },
  { value: 'run', label: 'Running' },
  { value: 'trail', label: 'Trail' },
  { value: 'hike', label: 'Hiking' },
  { value: 'cycle', label: 'Cycling' },
  { value: 'walk', label: 'Walking' },
  { value: 'swim', label: 'Swimming' },
  { value: 'other', label: 'Other' },
];

export default function ActivityList() {
  const [page, setPage] = useState(1);
  const [typeFilter, setTypeFilter] = useState('');
  const { data: activities, isLoading, error } = useActivities(page, typeFilter || undefined);

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold text-white tracking-tight">Activities</h1>
          <p className="text-slate-400 mt-1">Browse and manage your recorded activities</p>
        </div>
        <div className="flex items-center gap-3">
          {/* Type Filter */}
          <select
            value={typeFilter}
            onChange={(e) => {
              setTypeFilter(e.target.value);
              setPage(1);
            }}
            className="bg-[#16213e] text-slate-300 border border-slate-700 rounded-lg px-4 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500 focus:border-transparent appearance-none cursor-pointer"
          >
            {activityTypes.map((t) => (
              <option key={t.value} value={t.value}>
                {t.label}
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
            Upload
          </Link>
        </div>
      </div>

      {/* Loading */}
      {isLoading && (
        <div className="flex items-center justify-center h-64">
          <div className="animate-spin rounded-full h-10 w-10 border-t-2 border-b-2 border-cyan-400" />
        </div>
      )}

      {/* Error */}
      {error && (
        <div className="bg-red-900/20 border border-red-800 rounded-xl p-4">
          <p className="text-red-400">Failed to load activities: {error.message}</p>
        </div>
      )}

      {/* Activity Cards */}
      {activities && activities.length === 0 && (
        <div className="text-center py-16">
          <svg className="w-16 h-16 mx-auto text-slate-600 mb-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M9 20l-5.447-2.724A1 1 0 013 16.382V5.618a1 1 0 011.447-.894L9 7m0 13l6-3m-6 3V7m6 10l4.553 2.276A1 1 0 0021 18.382V7.618a1 1 0 00-.553-.894L15 4m0 13V4m0 0L9 7" />
          </svg>
          <p className="text-slate-400 text-lg">No activities found</p>
          <p className="text-slate-500 text-sm mt-1">Upload a GPX file to get started</p>
        </div>
      )}

      {activities && activities.length > 0 && (
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
          {activities.map((activity) => {
            const color = ACTIVITY_COLORS[activity.activityType] || ACTIVITY_COLORS.other;
            const label = ACTIVITY_LABELS[activity.activityType] || activity.activityType;

            return (
              <Link
                key={activity.id}
                to={`/activities/${activity.id}`}
                className="bg-[#16213e] rounded-2xl p-5 border border-slate-700/50 hover:border-slate-600 transition-all hover:shadow-lg hover:shadow-black/20 group"
              >
                {/* Card Header */}
                <div className="flex items-start justify-between mb-4">
                  <div className="min-w-0 flex-1">
                    <h3 className="text-white font-semibold truncate group-hover:text-cyan-300 transition-colors">
                      {activity.name}
                    </h3>
                    <p className="text-xs text-slate-500 mt-1">{formatDate(activity.startTime)}</p>
                  </div>
                  <span
                    className="text-xs font-bold px-2.5 py-1 rounded-full shrink-0 ml-3"
                    style={{
                      backgroundColor: color + '22',
                      color: color,
                    }}
                  >
                    {label}
                  </span>
                </div>

                {/* Stats Row */}
                <div className="grid grid-cols-3 gap-3">
                  <div>
                    <p className="text-xs text-slate-500 mb-0.5">Distance</p>
                    <p className="text-sm font-semibold text-white">
                      {activity.distanceKm.toFixed(1)} km
                    </p>
                  </div>
                  <div>
                    <p className="text-xs text-slate-500 mb-0.5">Elevation D+</p>
                    <p className="text-sm font-semibold text-white">
                      {Math.round(activity.elevationGainM)} m
                    </p>
                  </div>
                  <div>
                    <p className="text-xs text-slate-500 mb-0.5">Time</p>
                    <p className="text-sm font-semibold text-white">
                      {formatDuration(activity.movingTimeSeconds)}
                    </p>
                  </div>
                </div>

                {/* Status Indicator */}
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
                  <span className="text-xs text-slate-500">{activity.status}</span>
                  {activity.source !== 'upload' && (
                    <span className="text-xs text-slate-600 ml-auto">
                      via {activity.source}
                    </span>
                  )}
                </div>
              </Link>
            );
          })}
        </div>
      )}

      {/* Pagination */}
      {activities && activities.length > 0 && (
        <div className="flex items-center justify-center gap-3 pt-4">
          <button
            onClick={() => setPage((p) => Math.max(1, p - 1))}
            disabled={page === 1}
            className="px-4 py-2 rounded-lg bg-[#16213e] border border-slate-700 text-slate-300 text-sm disabled:opacity-40 disabled:cursor-not-allowed hover:bg-slate-700/50 transition-colors"
          >
            Previous
          </button>
          <span className="text-slate-400 text-sm">Page {page}</span>
          <button
            onClick={() => setPage((p) => p + 1)}
            disabled={activities.length < 20}
            className="px-4 py-2 rounded-lg bg-[#16213e] border border-slate-700 text-slate-300 text-sm disabled:opacity-40 disabled:cursor-not-allowed hover:bg-slate-700/50 transition-colors"
          >
            Next
          </button>
        </div>
      )}
    </div>
  );
}
