import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useActivity } from '../hooks/useActivities';
import { api } from '../api/client';
import { ACTIVITY_COLORS, ACTIVITY_LABELS } from '../types/activity';
import type { TrackReport } from '../types/activity';
import TrackMap from '../components/map/TrackMap';

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('en-US', {
    weekday: 'long',
    month: 'long',
    day: 'numeric',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

const STATUS_CONFIG: Record<string, { color: string; bg: string; label: string; pulse?: boolean }> = {
  Pending: { color: 'text-slate-400', bg: 'bg-slate-400', label: 'Pending' },
  Analyzing: { color: 'text-amber-400', bg: 'bg-amber-400', label: 'Analyzing GPX', pulse: true },
  AiProcessing: { color: 'text-purple-400', bg: 'bg-purple-400', label: 'AI Processing', pulse: true },
  Completed: { color: 'text-green-400', bg: 'bg-green-400', label: 'Completed' },
  Failed: { color: 'text-red-400', bg: 'bg-red-400', label: 'Failed' },
};

const DIFFICULTY_COLORS: Record<string, string> = {
  easy: 'bg-green-500/20 text-green-400 border-green-500/30',
  moderate: 'bg-amber-500/20 text-amber-400 border-amber-500/30',
  hard: 'bg-orange-500/20 text-orange-400 border-orange-500/30',
  expert: 'bg-red-500/20 text-red-400 border-red-500/30',
};

/** Radial gauge-style stat card */
function RadialStat({
  label,
  value,
  unit,
  percentage,
  color,
}: {
  label: string;
  value: string;
  unit: string;
  percentage: number;
  color: string;
}) {
  const radius = 40;
  const circumference = 2 * Math.PI * radius;
  const offset = circumference - (Math.min(percentage, 100) / 100) * circumference;

  return (
    <div className="bg-[#16213e] rounded-2xl p-5 border border-slate-700/50 flex flex-col items-center">
      <div className="relative w-24 h-24 mb-3">
        <svg className="w-24 h-24 -rotate-90" viewBox="0 0 100 100">
          <circle
            cx="50"
            cy="50"
            r={radius}
            fill="none"
            stroke="#334155"
            strokeWidth="6"
          />
          <circle
            cx="50"
            cy="50"
            r={radius}
            fill="none"
            stroke={color}
            strokeWidth="6"
            strokeLinecap="round"
            strokeDasharray={circumference}
            strokeDashoffset={offset}
            className="transition-all duration-1000"
          />
        </svg>
        <div className="absolute inset-0 flex items-center justify-center">
          <span className="text-lg font-bold text-white">{value}</span>
        </div>
      </div>
      <p className="text-xs text-slate-400">{unit}</p>
      <p className="text-sm font-medium text-slate-300 mt-1">{label}</p>
    </div>
  );
}

function AiReportSection({ report }: { report: TrackReport }) {
  const difficultyGrade = report.difficulty.grade.toLowerCase();
  const difficultyClass = DIFFICULTY_COLORS[difficultyGrade] || DIFFICULTY_COLORS.moderate;

  return (
    <div className="bg-[#16213e] rounded-2xl p-6 border border-slate-700/50 space-y-6">
      <div className="flex items-center justify-between">
        <h2 className="text-xl font-semibold text-white flex items-center gap-3">
          <svg className="w-6 h-6 text-purple-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z" />
          </svg>
          AI Analysis Report
        </h2>
        <span className={`text-sm font-bold px-3 py-1 rounded-full border ${difficultyClass}`}>
          {report.difficulty.grade} ({report.difficulty.score}/10)
        </span>
      </div>

      {/* Summary */}
      <div>
        <h3 className="text-sm font-medium text-slate-400 mb-2">Summary</h3>
        <p className="text-slate-300 leading-relaxed">{report.summary}</p>
      </div>

      {/* Difficulty Justification */}
      <div>
        <h3 className="text-sm font-medium text-slate-400 mb-2">Difficulty Assessment</h3>
        <p className="text-slate-300 text-sm">{report.difficulty.justification}</p>
      </div>

      {/* Effort */}
      {report.effort && (
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
          <div className="bg-slate-800/50 rounded-xl p-4">
            <p className="text-xs text-slate-500 mb-1">Fitness Level</p>
            <p className="text-sm font-semibold text-white">{report.effort.fitness_level}</p>
          </div>
          <div className="bg-slate-800/50 rounded-xl p-4">
            <p className="text-xs text-slate-500 mb-1">Estimated Duration</p>
            <p className="text-sm font-semibold text-white">{report.effort.estimated_duration}</p>
          </div>
          {report.effort.calorie_estimate && (
            <div className="bg-slate-800/50 rounded-xl p-4">
              <p className="text-xs text-slate-500 mb-1">Calories</p>
              <p className="text-sm font-semibold text-white">
                ~{report.effort.calorie_estimate} kcal
              </p>
            </div>
          )}
        </div>
      )}

      {/* Key Segments */}
      {report.key_segments && report.key_segments.length > 0 && (
        <div>
          <h3 className="text-sm font-medium text-slate-400 mb-3">Key Segments</h3>
          <div className="space-y-2">
            {report.key_segments.map((seg, i) => (
              <div
                key={i}
                className="flex items-start gap-3 bg-slate-800/30 rounded-xl p-3"
              >
                <span className="text-xs font-bold text-cyan-400 bg-cyan-400/10 px-2 py-1 rounded shrink-0 uppercase">
                  {seg.type}
                </span>
                <div className="min-w-0 flex-1">
                  <p className="text-sm text-slate-300">{seg.description}</p>
                  <div className="flex gap-4 mt-1">
                    {seg.distance_km != null && (
                      <span className="text-xs text-slate-500">{seg.distance_km} km</span>
                    )}
                    {seg.elevation_change != null && (
                      <span className="text-xs text-slate-500">
                        {seg.elevation_change > 0 ? '+' : ''}
                        {seg.elevation_change} m
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
          <h3 className="text-sm font-medium text-slate-400 mb-3">Recommendations</h3>
          <ul className="space-y-2">
            {report.recommendations.map((rec, i) => (
              <li key={i} className="flex items-start gap-2 text-sm text-slate-300">
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

export default function ActivityDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { data: activity, isLoading, error } = useActivity(id!);
  const [isDeleting, setIsDeleting] = useState(false);
  const [isReanalyzing, setIsReanalyzing] = useState(false);

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
        <p className="text-red-400 text-lg">Failed to load activity: {error.message}</p>
      </div>
    );
  }

  if (!activity) return null;

  const statusCfg = STATUS_CONFIG[activity.status] || STATUS_CONFIG.Pending;
  const color = ACTIVITY_COLORS[activity.activityType] || ACTIVITY_COLORS.other;
  const label = ACTIVITY_LABELS[activity.activityType] || activity.activityType;
  const stats = activity.stats;

  const handleDelete = async () => {
    if (!confirm('Are you sure you want to delete this activity?')) return;
    setIsDeleting(true);
    try {
      await api.deleteActivity(activity.id);
      navigate('/activities');
    } catch {
      setIsDeleting(false);
    }
  };

  const handleReanalyze = async () => {
    setIsReanalyzing(true);
    try {
      await api.reanalyzeActivity(activity.id);
    } finally {
      setIsReanalyzing(false);
    }
  };

  // Gauge percentages (normalized to reasonable maxima for display)
  const distPct = stats ? Math.min((stats.total_distance_km / 50) * 100, 100) : 0;
  const gainPct = stats ? Math.min((stats.elevation_gain_m / 3000) * 100, 100) : 0;
  const lossPct = stats ? Math.min((stats.elevation_loss_m / 3000) * 100, 100) : 0;
  const speedPct = stats ? Math.min((stats.avg_moving_speed_kmh / 30) * 100, 100) : 0;
  const timePct = stats ? Math.min((stats.moving_time.seconds / 28800) * 100, 100) : 0;

  return (
    <div className="space-y-6">
      {/* Back Button & Header */}
      <div className="flex items-start justify-between gap-4">
        <div>
          <button
            onClick={() => navigate(-1)}
            className="text-slate-400 hover:text-white transition-colors text-sm flex items-center gap-1 mb-3"
          >
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
            </svg>
            Back
          </button>
          <h1 className="text-3xl font-bold text-white tracking-tight">{activity.name}</h1>
          <div className="flex items-center gap-3 mt-2">
            <span
              className="text-xs font-bold px-2.5 py-1 rounded-full"
              style={{ backgroundColor: color + '22', color }}
            >
              {label}
            </span>
            <div className="flex items-center gap-2">
              <div className={`w-2 h-2 rounded-full ${statusCfg.bg} ${statusCfg.pulse ? 'animate-pulse' : ''}`} />
              <span className={`text-sm ${statusCfg.color}`}>{statusCfg.label}</span>
            </div>
            <span className="text-sm text-slate-500">{formatDate(activity.startTime)}</span>
          </div>
          {activity.errorMessage && (
            <p className="text-red-400 text-sm mt-2 bg-red-900/20 border border-red-800 rounded-lg px-3 py-2">
              {activity.errorMessage}
            </p>
          )}
        </div>

        {/* Actions */}
        <div className="flex items-center gap-2 shrink-0">
          <a
            href={api.getGpxUrl(activity.id)}
            download
            className="px-3 py-2 rounded-lg bg-[#16213e] border border-slate-700 text-slate-300 text-sm hover:bg-slate-700/50 transition-colors flex items-center gap-2"
          >
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
            </svg>
            GPX
          </a>
          <button
            onClick={handleReanalyze}
            disabled={isReanalyzing || activity.status === 'Analyzing' || activity.status === 'AiProcessing'}
            className="px-3 py-2 rounded-lg bg-purple-600 hover:bg-purple-500 text-white text-sm disabled:opacity-40 disabled:cursor-not-allowed transition-colors flex items-center gap-2"
          >
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
            {isReanalyzing ? 'Reanalyzing...' : 'Reanalyze'}
          </button>
          <button
            onClick={handleDelete}
            disabled={isDeleting}
            className="px-3 py-2 rounded-lg bg-red-600/20 border border-red-800 text-red-400 text-sm hover:bg-red-600/30 disabled:opacity-40 transition-colors flex items-center gap-2"
          >
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
            </svg>
            {isDeleting ? 'Deleting...' : 'Delete'}
          </button>
        </div>
      </div>

      {/* Track Map */}
      <div className="h-[500px] rounded-2xl overflow-hidden">
        <TrackMap gpxUrl={api.getGpxUrl(activity.id)} />
      </div>

      {/* Stats Grid - Radial Gauges */}
      {stats && (
        <div>
          <h2 className="text-xl font-semibold text-white mb-4">Performance Stats</h2>
          <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-4">
            <RadialStat
              label="Distance"
              value={stats.total_distance_km.toFixed(1)}
              unit="km"
              percentage={distPct}
              color="#00d4ff"
            />
            <RadialStat
              label="Elevation D+"
              value={Math.round(stats.elevation_gain_m).toString()}
              unit="meters"
              percentage={gainPct}
              color="#00ff88"
            />
            <RadialStat
              label="Elevation D-"
              value={Math.round(stats.elevation_loss_m).toString()}
              unit="meters"
              percentage={lossPct}
              color="#ff6b6b"
            />
            <RadialStat
              label="Avg Speed"
              value={stats.avg_moving_speed_kmh.toFixed(1)}
              unit="km/h"
              percentage={speedPct}
              color="#ff8800"
            />
            <RadialStat
              label="Moving Time"
              value={stats.moving_time.display}
              unit=""
              percentage={timePct}
              color="#aa88ff"
            />
          </div>
        </div>
      )}

      {/* Extended Stats */}
      {stats && (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          <div className="bg-[#16213e] rounded-xl p-4 border border-slate-700/50">
            <p className="text-xs text-slate-500 mb-1">Max Speed</p>
            <p className="text-lg font-bold text-white">{stats.max_speed_kmh.toFixed(1)} km/h</p>
          </div>
          <div className="bg-[#16213e] rounded-xl p-4 border border-slate-700/50">
            <p className="text-xs text-slate-500 mb-1">Avg Pace</p>
            <p className="text-lg font-bold text-white">{stats.avg_moving_pace}</p>
          </div>
          <div className="bg-[#16213e] rounded-xl p-4 border border-slate-700/50">
            <p className="text-xs text-slate-500 mb-1">Max Elevation</p>
            <p className="text-lg font-bold text-white">{Math.round(stats.max_elevation_m)} m</p>
          </div>
          <div className="bg-[#16213e] rounded-xl p-4 border border-slate-700/50">
            <p className="text-xs text-slate-500 mb-1">Min Elevation</p>
            <p className="text-lg font-bold text-white">{Math.round(stats.min_elevation_m)} m</p>
          </div>
          <div className="bg-[#16213e] rounded-xl p-4 border border-slate-700/50">
            <p className="text-xs text-slate-500 mb-1">Total Time</p>
            <p className="text-lg font-bold text-white">{stats.total_time.display}</p>
          </div>
          <div className="bg-[#16213e] rounded-xl p-4 border border-slate-700/50">
            <p className="text-xs text-slate-500 mb-1">Stopped Time</p>
            <p className="text-lg font-bold text-white">{stats.stopped_time.display}</p>
          </div>
          <div className="bg-[#16213e] rounded-xl p-4 border border-slate-700/50">
            <p className="text-xs text-slate-500 mb-1">Stops</p>
            <p className="text-lg font-bold text-white">{stats.stop_count}</p>
          </div>
          <div className="bg-[#16213e] rounded-xl p-4 border border-slate-700/50">
            <p className="text-xs text-slate-500 mb-1">Points / km</p>
            <p className="text-lg font-bold text-white">{Math.round(stats.points_per_km)}</p>
          </div>

          {/* Optional sensor stats */}
          {stats.heart_rate && (
            <>
              <div className="bg-[#16213e] rounded-xl p-4 border border-slate-700/50">
                <p className="text-xs text-slate-500 mb-1">Avg HR</p>
                <p className="text-lg font-bold text-red-400">{stats.heart_rate.avg_bpm} bpm</p>
              </div>
              <div className="bg-[#16213e] rounded-xl p-4 border border-slate-700/50">
                <p className="text-xs text-slate-500 mb-1">Max HR</p>
                <p className="text-lg font-bold text-red-400">{stats.heart_rate.max_bpm} bpm</p>
              </div>
            </>
          )}
          {stats.power && (
            <div className="bg-[#16213e] rounded-xl p-4 border border-slate-700/50">
              <p className="text-xs text-slate-500 mb-1">Avg Power</p>
              <p className="text-lg font-bold text-yellow-400">{stats.power.avg_watts} W</p>
            </div>
          )}
          {stats.cadence && (
            <div className="bg-[#16213e] rounded-xl p-4 border border-slate-700/50">
              <p className="text-xs text-slate-500 mb-1">Avg Cadence</p>
              <p className="text-lg font-bold text-blue-400">{stats.cadence.avg_rpm} rpm</p>
            </div>
          )}
        </div>
      )}

      {/* AI Report */}
      {activity.aiReport && <AiReportSection report={activity.aiReport} />}

      {/* Processing indicator when not yet complete */}
      {(activity.status === 'Analyzing' || activity.status === 'AiProcessing') && (
        <div className="bg-[#16213e] rounded-2xl p-8 border border-slate-700/50 text-center">
          <div className="animate-spin rounded-full h-10 w-10 border-t-2 border-b-2 border-purple-400 mx-auto mb-4" />
          <p className="text-slate-300 font-medium">
            {activity.status === 'Analyzing' ? 'Analyzing GPX track data...' : 'AI is processing your activity...'}
          </p>
          <p className="text-slate-500 text-sm mt-1">This page will refresh automatically.</p>
        </div>
      )}
    </div>
  );
}
