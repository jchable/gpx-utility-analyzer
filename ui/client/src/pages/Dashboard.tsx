import { Link } from 'react-router-dom';
import { useDashboard } from '../hooks/useActivities';
import { ACTIVITY_COLORS, ACTIVITY_LABELS } from '../types/activity';

function formatDuration(seconds: number): string {
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  if (h > 0) return `${h}h ${m}m`;
  return `${m}m`;
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  });
}

export default function Dashboard() {
  const { data, isLoading, error } = useDashboard();

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-96">
        <div className="animate-spin rounded-full h-12 w-12 border-t-2 border-b-2 border-cyan-400" />
      </div>
    );
  }

  if (error) {
    return (
      <div className="flex items-center justify-center h-96">
        <p className="text-red-400 text-lg">Failed to load dashboard: {error.message}</p>
      </div>
    );
  }

  if (!data) return null;

  const statWidgets = [
    {
      label: 'Total Activities',
      value: data.totalActivities.toString(),
      icon: (
        <svg className="w-8 h-8 text-cyan-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
        </svg>
      ),
      accent: 'text-cyan-400',
    },
    {
      label: 'Distance This Month',
      value: `${data.distanceThisMonthKm.toFixed(1)} km`,
      icon: (
        <svg className="w-8 h-8 text-green-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 7h8m0 0v8m0-8l-8 8-4-4-6 6" />
        </svg>
      ),
      accent: 'text-green-400',
    },
    {
      label: 'D+ This Month',
      value: `${Math.round(data.totalElevationGainM)} m`,
      icon: (
        <svg className="w-8 h-8 text-amber-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 10l7-7m0 0l7 7m-7-7v18" />
        </svg>
      ),
      accent: 'text-amber-400',
    },
    {
      label: 'Time This Month',
      value: formatDuration(data.totalMovingTimeSeconds),
      icon: (
        <svg className="w-8 h-8 text-purple-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
        </svg>
      ),
      accent: 'text-purple-400',
    },
  ];

  // Activity type breakdown
  const breakdown = data.activityTypeBreakdown;
  const breakdownEntries = Object.entries(breakdown).sort(([, a], [, b]) => b - a);
  const totalBreakdown = breakdownEntries.reduce((sum, [, count]) => sum + count, 0);

  // Build conic gradient for donut chart
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
      {/* Page Header */}
      <div>
        <h1 className="text-3xl font-bold text-white tracking-tight">Dashboard</h1>
        <p className="text-slate-400 mt-1">Your activity overview at a glance</p>
      </div>

      {/* Stat Widgets Grid */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-5">
        {statWidgets.map((widget) => (
          <div
            key={widget.label}
            className="bg-[#16213e] rounded-2xl p-6 border border-slate-700/50 hover:border-slate-600 transition-colors"
          >
            <div className="flex items-center justify-between mb-4">
              <span className="text-slate-400 text-sm font-medium">{widget.label}</span>
              {widget.icon}
            </div>
            <p className={`text-3xl font-bold ${widget.accent}`}>{widget.value}</p>
          </div>
        ))}
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Activity Type Breakdown */}
        <div className="bg-[#16213e] rounded-2xl p-6 border border-slate-700/50">
          <h2 className="text-lg font-semibold text-white mb-6">Activity Breakdown</h2>
          <div className="flex flex-col items-center gap-6">
            {/* Donut Chart */}
            <div className="relative w-40 h-40 shrink-0">
              <div
                className="w-full h-full rounded-full"
                style={{ background: totalBreakdown > 0 ? conicGradient : '#334155' }}
              />
              <div className="absolute inset-5 bg-[#16213e] rounded-full flex items-center justify-center">
                <div className="text-center">
                  <p className="text-2xl font-bold text-white">{totalBreakdown}</p>
                  <p className="text-xs text-slate-400">total</p>
                </div>
              </div>
            </div>
            {/* Legend */}
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
                      <span className="text-sm text-slate-300">
                        {ACTIVITY_LABELS[type] || type}
                      </span>
                    </div>
                    <div className="flex items-center gap-2">
                      <span className="text-sm font-medium text-white">{count}</span>
                      <span className="text-xs text-slate-500">({pct}%)</span>
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        </div>

        {/* Recent Activities */}
        <div className="lg:col-span-2 bg-[#16213e] rounded-2xl p-6 border border-slate-700/50">
          <div className="flex items-center justify-between mb-6">
            <h2 className="text-lg font-semibold text-white">Recent Activities</h2>
            <Link
              to="/activities"
              className="text-sm text-cyan-400 hover:text-cyan-300 transition-colors"
            >
              View all
            </Link>
          </div>
          <div className="space-y-2">
            {data.recentActivities.length === 0 && (
              <p className="text-slate-500 text-center py-8">
                No activities yet. Upload a GPX file to get started.
              </p>
            )}
            {data.recentActivities.map((activity) => (
              <Link
                key={activity.id}
                to={`/activities/${activity.id}`}
                className="flex items-center justify-between p-3 rounded-xl hover:bg-slate-700/30 transition-colors group"
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
                    {(ACTIVITY_LABELS[activity.activityType] || activity.activityType)
                      .slice(0, 3)
                      .toUpperCase()}
                  </div>
                  <div className="min-w-0">
                    <p className="text-white font-medium group-hover:text-cyan-300 transition-colors truncate">
                      {activity.name}
                    </p>
                    <p className="text-xs text-slate-500">{formatDate(activity.startTime)}</p>
                  </div>
                </div>
                <div className="flex items-center gap-6 text-sm text-slate-400 shrink-0 ml-4">
                  <span>{activity.distanceKm.toFixed(1)} km</span>
                  <span className="hidden sm:inline">{activity.elevationGainM} m D+</span>
                  <span className="hidden md:inline">{formatDuration(activity.movingTimeSeconds)}</span>
                </div>
              </Link>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
