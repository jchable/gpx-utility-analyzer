import { useState, useMemo } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { useActivity, useProfile, useTrack, useSplits, useSettings } from '../hooks/useActivities';
import { api } from '../api/client';
import { routesApi } from '../api/routes-client';
import { ACTIVITY_COLORS } from '../types/activity';
import type { TrackReport } from '../types/activity';
import TrackMap from '../components/map/TrackMap';
import ElevationProfileChart from '../components/activity/ElevationProfileChart';
import HRZonesSection from '../components/activity/HRZonesSection';
import PowerZonesSection from '../components/activity/PowerZonesSection';
import AnomalyBanner from '../components/activity/AnomalyBanner';
import StopsTable from '../components/activity/StopsTable';
import SplitsSection from '../components/activity/SplitsSection';
import EffortComparisonSection from '../components/activity/EffortComparisonSection';
import { getEffectiveMaxHR, computeHRZones, computePowerZones, computeTRIMP, computePowerMetrics } from '../utils/zones';
import { formatDuration } from '../utils/format';

const STATUS_CONFIG: Record<string, { color: string; bg: string; pulse?: boolean }> = {
  Pending: { color: 'text-slate-400', bg: 'bg-slate-400' },
  Analyzing: { color: 'text-amber-400', bg: 'bg-amber-400', pulse: true },
  AiProcessing: { color: 'text-purple-400', bg: 'bg-purple-400', pulse: true },
  Completed: { color: 'text-green-400', bg: 'bg-green-400' },
  Failed: { color: 'text-red-400', bg: 'bg-red-400' },
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
          <circle cx="50" cy="50" r={radius} fill="none" stroke="#334155" strokeWidth="6" />
          <circle
            cx="50" cy="50" r={radius} fill="none"
            stroke={color} strokeWidth="6" strokeLinecap="round"
            strokeDasharray={circumference} strokeDashoffset={offset}
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

/** Dual-arc gauge showing D+ (green) and D- (red) proportions */
function ElevationGauge({
  gain,
  loss,
  label,
  unitLabel,
}: {
  gain: number;
  loss: number;
  label: string;
  unitLabel: string;
}) {
  const radius = 40;
  const circumference = 2 * Math.PI * radius;
  const total = gain + loss;
  const gainPct = total > 0 ? gain / total : 0.5;
  const gainArc = gainPct * circumference;
  const lossArc = (1 - gainPct) * circumference;

  return (
    <div className="bg-[#16213e] rounded-2xl p-5 border border-slate-700/50 flex flex-col items-center">
      <div className="relative w-24 h-24 mb-3">
        <svg className="w-24 h-24 -rotate-90" viewBox="0 0 100 100">
          <circle cx="50" cy="50" r={radius} fill="none" stroke="#334155" strokeWidth="6" />
          {/* D+ green arc from top */}
          <circle
            cx="50" cy="50" r={radius} fill="none"
            stroke="#00ff88" strokeWidth="6"
            strokeDasharray={circumference} strokeDashoffset={circumference - gainArc}
            className="transition-all duration-1000"
          />
          {/* D- red arc continuing after green */}
          <circle
            cx="50" cy="50" r={radius} fill="none"
            stroke="#ff6b6b" strokeWidth="6"
            strokeDasharray={circumference} strokeDashoffset={circumference - lossArc}
            className="transition-all duration-1000"
            style={{ transform: `rotate(${gainPct * 360}deg)`, transformOrigin: '50px 50px' }}
          />
        </svg>
        <div className="absolute inset-0 flex flex-col items-center justify-center">
          <span className="text-sm font-bold text-[#00ff88] leading-tight">+{Math.round(gain)}</span>
          <span className="text-sm font-bold text-[#ff6b6b] leading-tight">&minus;{Math.round(loss)}</span>
        </div>
      </div>
      <p className="text-xs text-slate-400">{unitLabel}</p>
      <p className="text-sm font-medium text-slate-300 mt-1">{label}</p>
    </div>
  );
}

function AiReportSection({ report }: { report: TrackReport }) {
  const { t } = useTranslation('activities');
  const { t: tc } = useTranslation();

  const difficultyGrade = report.difficulty.grade.toLowerCase();
  const difficultyClass = DIFFICULTY_COLORS[difficultyGrade] || DIFFICULTY_COLORS.moderate;

  return (
    <div className="bg-[#16213e] rounded-2xl p-6 border border-slate-700/50 space-y-6">
      <div className="flex items-center justify-between">
        <h2 className="text-xl font-semibold text-white flex items-center gap-3">
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
        <h3 className="text-sm font-medium text-slate-400 mb-2">{t('aiReport.summary')}</h3>
        <p className="text-slate-300 leading-relaxed">{report.summary}</p>
      </div>

      {/* Difficulty Justification */}
      <div>
        <h3 className="text-sm font-medium text-slate-400 mb-2">{t('aiReport.difficultyAssessment')}</h3>
        <p className="text-slate-300 text-sm">{report.difficulty.justification}</p>
      </div>

      {/* Effort */}
      {report.effort && (
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
          <div className="bg-slate-800/50 rounded-xl p-4">
            <p className="text-xs text-slate-500 mb-1">{t('aiReport.fitnessLevel')}</p>
            <p className="text-sm font-semibold text-white">{report.effort.fitness_level}</p>
          </div>
          <div className="bg-slate-800/50 rounded-xl p-4">
            <p className="text-xs text-slate-500 mb-1">{t('aiReport.estimatedDuration')}</p>
            <p className="text-sm font-semibold text-white">{report.effort.estimated_duration}</p>
          </div>
          {report.effort.calorie_estimate && (
            <div className="bg-slate-800/50 rounded-xl p-4">
              <p className="text-xs text-slate-500 mb-1">{t('aiReport.calories')}</p>
              <p className="text-sm font-semibold text-white">
                ~{report.effort.calorie_estimate} {tc('unit.kcal')}
              </p>
            </div>
          )}
        </div>
      )}

      {/* Key Segments */}
      {report.key_segments && report.key_segments.length > 0 && (
        <div>
          <h3 className="text-sm font-medium text-slate-400 mb-3">{t('aiReport.keySegments')}</h3>
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
                      <span className="text-xs text-slate-500">{seg.distance_km} {tc('unit.km')}</span>
                    )}
                    {seg.elevation_change != null && (
                      <span className="text-xs text-slate-500">
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
          <h3 className="text-sm font-medium text-slate-400 mb-3">{t('aiReport.recommendations')}</h3>
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
  const queryClient = useQueryClient();
  const { t } = useTranslation('activities');
  const { t: tc } = useTranslation();
  const { i18n } = useTranslation();
  const { data: activity, isLoading, error } = useActivity(id!);
  const isCompleted = activity?.status === 'Completed';
  const { data: profileData } = useProfile(isCompleted ? id! : '');
  const { data: trackData, isLoading: trackLoading, error: trackError } = useTrack(isCompleted ? id! : '');
  const { data: splitsData } = useSplits(isCompleted ? id! : '');
  const { data: settings } = useSettings();
  const { t: tRoutes } = useTranslation('routes');
  const [isDeleting, setIsDeleting] = useState(false);
  const [isReanalyzing, setIsReanalyzing] = useState(false);
  const [isCreatingRoute, setIsCreatingRoute] = useState(false);
  const [focusedStop, setFocusedStop] = useState<{ lat: number; lon: number } | null>(null);

  const hasTimestamps = (profileData?.length ?? 0) > 0 && profileData![0].elapsedTime != null;

  // Zone computations (client-side from profile data + user settings)
  const effectiveMaxHR = useMemo(
    () => getEffectiveMaxHR(settings?.athlete, activity?.stats?.heart_rate?.max_bpm),
    [settings?.athlete, activity?.stats?.heart_rate?.max_bpm],
  );

  const hrMaxSource = useMemo((): 'user' | 'age' | 'observed' => {
    if (settings?.athlete?.maxHeartRate && settings.athlete.maxHeartRate > 0) return 'user';
    if (settings?.athlete?.age && settings.athlete.age > 0) return 'age';
    return 'observed';
  }, [settings?.athlete]);

  const hrZones = useMemo(
    () => (profileData && effectiveMaxHR ? computeHRZones(profileData, effectiveMaxHR) : null),
    [profileData, effectiveMaxHR],
  );

  const trimp = useMemo(() => (hrZones ? computeTRIMP(hrZones) : 0), [hrZones]);

  const ftp = settings?.athlete?.ftp;

  const powerZones = useMemo(
    () => (profileData && ftp ? computePowerZones(profileData, ftp) : null),
    [profileData, ftp],
  );

  const powerMetrics = useMemo(
    () =>
      activity?.stats?.power && ftp
        ? computePowerMetrics(activity.stats.power, ftp, activity.stats.moving_time.seconds)
        : null,
    [activity?.stats?.power, ftp, activity?.stats?.moving_time.seconds],
  );

  const formatDate = (iso: string): string => {
    return new Date(iso).toLocaleDateString(i18n.language, {
      weekday: 'long',
      month: 'long',
      day: 'numeric',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

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
        <p className="text-red-400 text-lg">{t('detail.loadError', { message: error.message })}</p>
      </div>
    );
  }

  if (!activity) return null;

  const statusCfg = STATUS_CONFIG[activity.status] || STATUS_CONFIG.Pending;
  const color = ACTIVITY_COLORS[activity.activityType] || ACTIVITY_COLORS.other;
  const stats = activity.stats;

  const handleDelete = async () => {
    if (!confirm(t('detail.deleteConfirm'))) return;
    setIsDeleting(true);
    try {
      await api.deleteActivity(activity.id);
      queryClient.invalidateQueries({ queryKey: ['dashboard'] });
      queryClient.invalidateQueries({ queryKey: ['activities'] });
      navigate('/activities');
    } catch {
      setIsDeleting(false);
    }
  };

  const handleReanalyze = async () => {
    setIsReanalyzing(true);
    try {
      await api.reanalyzeActivity(activity.id);
      // Invalidate cache so polling restarts and picks up new status
      await queryClient.invalidateQueries({ queryKey: ['activity', id] });
    } finally {
      setIsReanalyzing(false);
    }
  };

  const handleEditAsRoute = async () => {
    setIsCreatingRoute(true);
    try {
      const route = await routesApi.createFromActivity(activity.id);
      if (route?.id) {
        navigate(`/editor/${route.id}`);
      }
    } catch (err) {
      console.error('Failed to create route from activity:', err);
      setIsCreatingRoute(false);
    }
  };

  // Gauge: moving time as percentage of total time
  const movingPct = stats && stats.total_time.seconds > 0
    ? Math.round((stats.moving_time.seconds / stats.total_time.seconds) * 100)
    : 0;

  return (
    <div className="space-y-6">
      {/* Back Button & Header */}
      <div className="flex flex-col sm:flex-row sm:items-start sm:justify-between gap-4">
        <div>
          <button
            onClick={() => navigate(-1)}
            className="text-slate-400 hover:text-white transition-colors text-sm flex items-center gap-1 mb-3"
          >
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
            </svg>
            {tc('button.back')}
          </button>
          <h1 className="text-3xl font-bold text-white tracking-tight">{activity.name}</h1>
          <div className="flex items-center gap-3 mt-2">
            <span
              className="text-xs font-bold px-2.5 py-1 rounded-full"
              style={{ backgroundColor: color + '22', color }}
            >
              {tc(`activityType.${activity.activityType}`)}
            </span>
            <div className="flex items-center gap-2">
              <div className={`w-2 h-2 rounded-full ${statusCfg.bg} ${statusCfg.pulse ? 'animate-pulse' : ''}`} />
              <span className={`text-sm ${statusCfg.color}`}>{tc(`status.${activity.status}`)}</span>
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
        <div className="flex items-center gap-2 flex-wrap">
          <a
            href={api.getGpxUrl(activity.id)}
            download
            className="px-3 py-2 rounded-lg bg-[#16213e] border border-slate-700 text-slate-300 text-sm hover:bg-slate-700/50 transition-colors flex items-center gap-2"
          >
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
            </svg>
            {t('detail.gpx')}
          </a>
          <button
            onClick={handleEditAsRoute}
            disabled={isCreatingRoute || activity.status !== 'Completed'}
            className="px-3 py-2 rounded-lg bg-[#16213e] border border-cyan-700 text-cyan-400 text-sm hover:bg-cyan-900/30 disabled:opacity-40 disabled:cursor-not-allowed transition-colors flex items-center gap-2"
          >
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
            </svg>
            {isCreatingRoute ? '...' : tRoutes('editAsRoute')}
          </button>
          <button
            onClick={handleReanalyze}
            disabled={isReanalyzing || activity.status === 'Analyzing' || activity.status === 'AiProcessing'}
            className="px-3 py-2 rounded-lg bg-purple-600 hover:bg-purple-500 text-white text-sm disabled:opacity-40 disabled:cursor-not-allowed transition-colors flex items-center gap-2"
          >
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
            {isReanalyzing ? t('detail.reanalyzing') : t('detail.reanalyze')}
          </button>
          <button
            onClick={handleDelete}
            disabled={isDeleting}
            className="px-3 py-2 rounded-lg bg-red-600/20 border border-red-800 text-red-400 text-sm hover:bg-red-600/30 disabled:opacity-40 transition-colors flex items-center gap-2"
          >
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
            </svg>
            {isDeleting ? t('detail.deleting') : tc('button.delete')}
          </button>
        </div>
      </div>

      {/* Anomaly Banner */}
      {stats?.anomalies && stats.anomalies.total_count > 0 && (
        <AnomalyBanner report={stats.anomalies} />
      )}

      {/* Track Map */}
      <div className="h-[300px] sm:h-[400px] lg:h-[500px] rounded-2xl overflow-hidden">
        <TrackMap coordinates={trackData?.coordinates} loading={trackLoading} error={trackError?.message} focusedPoint={focusedStop} />
      </div>

      {/* Elevation Profile */}
      {activity.status === 'Completed' && (
        <ElevationProfileChart
          data={profileData ?? []}
          stops={activity.stats?.stops}
          hasTimestamps={hasTimestamps}
          activityStartTime={activity.stats?.start_time}
        />
      )}

      {/* Ratio Gauges */}
      {stats && (
        <div>
          <h2 className="text-xl font-semibold text-white mb-4">{t('detail.performanceStats')}</h2>
          <div className="grid grid-cols-2 gap-4 max-w-md mx-auto">
            <RadialStat
              label={t('detail.movingRatio')}
              value={`${movingPct}`}
              unit="%"
              percentage={movingPct}
              color="#aa88ff"
            />
            <ElevationGauge
              gain={stats.elevation_gain_m}
              loss={stats.elevation_loss_m}
              label={t('detail.elevationChange')}
              unitLabel={tc('unit.meters')}
            />
          </div>
        </div>
      )}

      {/* Extended Stats */}
      {stats && (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          <div className="bg-[#16213e] rounded-xl p-4 border border-slate-700/50">
            <p className="text-xs text-slate-500 mb-1">{t('distance')}</p>
            <p className="text-lg font-bold text-cyan-400">{stats.total_distance_km.toFixed(1)} {tc('unit.km')}</p>
          </div>
          <div className="bg-[#16213e] rounded-xl p-4 border border-slate-700/50">
            <p className="text-xs text-slate-500 mb-1">{t('detail.avgSpeed')}</p>
            <p className="text-lg font-bold text-white">{stats.avg_moving_speed_kmh.toFixed(1)} {tc('unit.kmh')}</p>
          </div>
          <div className="bg-[#16213e] rounded-xl p-4 border border-slate-700/50">
            <p className="text-xs text-slate-500 mb-1">{t('detail.movingTime')}</p>
            <p className="text-lg font-bold text-white">{stats.moving_time.display}</p>
          </div>
          <div className="bg-[#16213e] rounded-xl p-4 border border-slate-700/50">
            <p className="text-xs text-slate-500 mb-1">{t('detail.avgPace')}</p>
            <p className="text-lg font-bold text-white">{stats.avg_moving_pace}</p>
          </div>
          <div className="bg-[#16213e] rounded-xl p-4 border border-slate-700/50">
            <p className="text-xs text-slate-500 mb-1">{t('detail.maxSpeed')}</p>
            <p className="text-lg font-bold text-white">{stats.max_speed_kmh.toFixed(1)} {tc('unit.kmh')}</p>
          </div>
          <div className="bg-[#16213e] rounded-xl p-4 border border-slate-700/50">
            <p className="text-xs text-slate-500 mb-1">{t('detail.maxElevation')}</p>
            <p className="text-lg font-bold text-white">{Math.round(stats.max_elevation_m)} {tc('unit.m')}</p>
          </div>
          <div className="bg-[#16213e] rounded-xl p-4 border border-slate-700/50">
            <p className="text-xs text-slate-500 mb-1">{t('detail.minElevation')}</p>
            <p className="text-lg font-bold text-white">{Math.round(stats.min_elevation_m)} {tc('unit.m')}</p>
          </div>
          <div className="bg-[#16213e] rounded-xl p-4 border border-slate-700/50">
            <p className="text-xs text-slate-500 mb-1">{t('detail.totalTime')}</p>
            <p className="text-lg font-bold text-white">{formatDuration(stats.total_time.seconds, tc)}</p>
          </div>
          <div className="bg-[#16213e] rounded-xl p-4 border border-slate-700/50">
            <p className="text-xs text-slate-500 mb-1">{t('detail.stoppedTime')}</p>
            <p className="text-lg font-bold text-white">{formatDuration(stats.stopped_time.seconds, tc)}</p>
          </div>
          <div className="bg-[#16213e] rounded-xl p-4 border border-slate-700/50">
            <p className="text-xs text-slate-500 mb-1">{t('detail.stops')}</p>
            <p className="text-lg font-bold text-white">{stats.stop_count}</p>
          </div>
          <div className="bg-[#16213e] rounded-xl p-4 border border-slate-700/50">
            <p className="text-xs text-slate-500 mb-1">{t('detail.pointsPerKm')}</p>
            <p className="text-lg font-bold text-white">{Math.round(stats.points_per_km)}</p>
          </div>

          {/* Optional sensor stats */}
          {stats.heart_rate && (
            <>
              <div className="bg-[#16213e] rounded-xl p-4 border border-slate-700/50">
                <p className="text-xs text-slate-500 mb-1">{t('detail.avgHR')}</p>
                <p className="text-lg font-bold text-red-400">{Math.round(stats.heart_rate.avg_bpm)} {tc('unit.bpm')}</p>
              </div>
              <div className="bg-[#16213e] rounded-xl p-4 border border-slate-700/50">
                <p className="text-xs text-slate-500 mb-1">{t('detail.maxHR')}</p>
                <p className="text-lg font-bold text-red-400">{stats.heart_rate.max_bpm} {tc('unit.bpm')}</p>
              </div>
            </>
          )}
          {stats.power && (
            <div className="bg-[#16213e] rounded-xl p-4 border border-slate-700/50">
              <p className="text-xs text-slate-500 mb-1">{t('detail.avgPower')}</p>
              <p className="text-lg font-bold text-yellow-400">{Math.round(stats.power.avg_watts)} {tc('unit.watts')}</p>
            </div>
          )}
          {stats.cadence && (
            <div className="bg-[#16213e] rounded-xl p-4 border border-slate-700/50">
              <p className="text-xs text-slate-500 mb-1">{t('detail.avgCadence')}</p>
              <p className="text-lg font-bold text-blue-400">{Math.round(stats.cadence.avg_rpm)} {tc('unit.rpm')}</p>
            </div>
          )}
        </div>
      )}

      {/* Effort Comparison */}
      {stats?.effort && <EffortComparisonSection effort={stats.effort} />}

      {/* HR Zones */}
      {hrZones && effectiveMaxHR && activity.stats?.heart_rate && (
        <HRZonesSection
          zones={hrZones}
          trimp={trimp}
          maxHR={effectiveMaxHR}
          source={hrMaxSource}
        />
      )}

      {/* Power Zones */}
      {powerZones && ftp && activity.stats?.power && powerMetrics && (
        <PowerZonesSection
          zones={powerZones}
          ftp={ftp}
          metrics={powerMetrics}
          avgWatts={activity.stats.power.avg_watts}
          maxWatts={activity.stats.power.max_watts}
        />
      )}

      {/* Stops Table */}
      {stats && stats.stops && stats.stops.length > 0 && (
        <StopsTable
          stops={stats.stops}
          activityStartTime={stats.start_time}
          totalStopTime={stats.total_stop_time}
          avgStopDuration={stats.avg_stop_duration}
          onStopClick={(lat, lon) => setFocusedStop({ lat, lon })}
        />
      )}

      {/* Splits & Best Efforts */}
      {splitsData && (
        <SplitsSection
          splits={splitsData.splits}
          bestEfforts={splitsData.bestEfforts}
          activityType={activity.activityType}
        />
      )}

      {/* AI Report */}
      {activity.aiReport && <AiReportSection report={activity.aiReport} />}

      {/* Processing indicator when not yet complete */}
      {(activity.status === 'Analyzing' || activity.status === 'AiProcessing') && (
        <div className="bg-[#16213e] rounded-2xl p-8 border border-slate-700/50 text-center">
          <div className="animate-spin rounded-full h-10 w-10 border-t-2 border-b-2 border-purple-400 mx-auto mb-4" />
          <p className="text-slate-300 font-medium">
            {activity.status === 'Analyzing' ? t('detail.analyzingGpx') : t('detail.aiProcessing')}
          </p>
          <p className="text-slate-500 text-sm mt-1">{t('detail.autoRefresh')}</p>
        </div>
      )}
    </div>
  );
}
