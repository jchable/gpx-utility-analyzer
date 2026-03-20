import { useState, useMemo, useEffect, useCallback } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { useActivity, useProfile, useTrack, useSplits, useSettings } from '../hooks/useActivities';
import { api } from '../api/client';
import { routesApi } from '../api/routes-client';
import { ACTIVITY_COLORS, ACTIVITY_TYPES } from '../types/activity';
import TrackMap from '../components/map/TrackMap';
import ElevationProfileChart from '../components/activity/ElevationProfileChart';
import HRZonesSection from '../components/activity/HRZonesSection';
import PowerZonesSection from '../components/activity/PowerZonesSection';
import AnomalyBanner from '../components/activity/AnomalyBanner';
import StopsTable from '../components/activity/StopsTable';
import SplitsSection from '../components/activity/SplitsSection';
import EffortComparisonSection from '../components/activity/EffortComparisonSection';
import AiReportSection from '../components/activity/AiReportSection';
import RadialStat from '../components/widgets/RadialStat';
import ElevationGauge from '../components/widgets/ElevationGauge';
import { getEffectiveMaxHR, computeHRZones, computePowerZones, computeTRIMP, computePowerMetrics } from '../utils/zones';
import { formatDuration, formatDate } from '../utils/format';

const STATUS_CONFIG: Record<string, { color: string; bg: string; pulse?: boolean }> = {
  Pending: { color: 'text-content-muted', bg: 'bg-content-muted' },
  Analyzing: { color: 'text-amber-400', bg: 'bg-amber-400', pulse: true },
  AiProcessing: { color: 'text-purple-400', bg: 'bg-purple-400', pulse: true },
  Completed: { color: 'text-green-400', bg: 'bg-green-400' },
  Failed: { color: 'text-red-400', bg: 'bg-red-400' },
};

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
  const [editingType, setEditingType] = useState(false);
  const [pendingType, setPendingType] = useState<string | null>(null);
  const [isChangingType, setIsChangingType] = useState(false);

  // Enrichment state (description, RPE, tags, sessionType)
  const [localDesc, setLocalDesc] = useState('');
  const [localRpe, setLocalRpe] = useState<number | null>(null);
  const [localSessionType, setLocalSessionType] = useState('');
  const [localTags, setLocalTags] = useState<string[]>([]);
  const [tagInput, setTagInput] = useState('');
  const [tagSuggestions, setTagSuggestions] = useState<string[]>([]);
  const [enrichSaving, setEnrichSaving] = useState(false);
  const [enrichSaved, setEnrichSaved] = useState(false);

  // Sync enrichment state when activity loads (only on ID change)
  useEffect(() => {
    if (activity) {
      setLocalDesc(activity.description ?? '');
      setLocalRpe(activity.perceivedExertion ?? null);
      setLocalSessionType(activity.sessionType ?? '');
      setLocalTags(activity.tags ?? []);
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [activity?.id]);

  // Load tag suggestions once
  useEffect(() => {
    api.getTags().then(setTagSuggestions).catch(() => {});
  }, []);

  const saveEnrichment = useCallback(async (patch: Parameters<typeof api.updateActivity>[1]) => {
    if (!activity) return;
    setEnrichSaving(true);
    setEnrichSaved(false);
    try {
      await api.updateActivity(activity.id, patch);
      queryClient.invalidateQueries({ queryKey: ['activity', id] });
      setEnrichSaved(true);
      setTimeout(() => setEnrichSaved(false), 2000);
    } finally {
      setEnrichSaving(false);
    }
  }, [activity, queryClient, id]);

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

  const detailDateOpts: Intl.DateTimeFormatOptions = {
    weekday: 'long', month: 'long', day: 'numeric', year: 'numeric',
    hour: '2-digit', minute: '2-digit',
  };

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
            className="text-content-muted hover:text-content transition-colors text-sm flex items-center gap-1 mb-3"
          >
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
            </svg>
            {tc('button.back')}
          </button>
          <h1 className="text-3xl font-bold text-content tracking-tight">{activity.name}</h1>
          <div className="flex items-center gap-3 mt-2">
            {editingType ? (
              <div className="flex items-center gap-1 flex-wrap">
                {ACTIVITY_TYPES.map((t) => {
                  const c = ACTIVITY_COLORS[t] || ACTIVITY_COLORS.other;
                  const isActive = t === activity.activityType;
                  return (
                    <button
                      key={t}
                      onClick={() => {
                        if (t !== activity.activityType) {
                          setPendingType(t);
                        }
                        setEditingType(false);
                      }}
                      className="text-xs font-bold px-2.5 py-1 rounded-full border transition-colors"
                      style={{
                        backgroundColor: isActive ? c + '33' : 'transparent',
                        color: isActive ? c : 'var(--content-muted)',
                        borderColor: isActive ? c + '55' : 'var(--ring-track)',
                      }}
                    >
                      {tc(`activityType.${t}`)}
                    </button>
                  );
                })}
              </div>
            ) : (
              <button
                onClick={() => setEditingType(true)}
                className="text-xs font-bold px-2.5 py-1 rounded-full cursor-pointer hover:ring-2 hover:ring-white/20 transition-all"
                style={{ backgroundColor: color + '22', color }}
                title={tc('button.edit') ?? 'Edit'}
              >
                {tc(`activityType.${activity.activityType}`)}
              </button>
            )}
            {activity.detectedSubType && (
              <span className="text-xs font-semibold px-2.5 py-1 rounded-full bg-amber-500/15 text-amber-400 border border-amber-500/30">
                {tc(`subType.${activity.detectedSubType}`, { defaultValue: activity.detectedSubType })}
              </span>
            )}
            <div className="flex items-center gap-2">
              <div className={`w-2 h-2 rounded-full ${statusCfg.bg} ${statusCfg.pulse ? 'animate-pulse' : ''}`} />
              <span className={`text-sm ${statusCfg.color}`}>{tc(`status.${activity.status}`)}</span>
            </div>
            <span className="text-sm text-content-muted">{formatDate(activity.startTime, i18n.language, detailDateOpts)}</span>
          </div>
          {activity.errorMessage && (
            <p className="text-red-400 text-sm mt-2 bg-red-900/20 border border-red-800 rounded-lg px-3 py-2">
              {activity.errorMessage}
            </p>
          )}
        </div>

        {/* Actions */}
        <div className="flex items-center gap-2 flex-wrap">
          <button
            onClick={() => api.downloadGpx(activity.id, activity.name)}
            className="px-3 py-2 rounded-lg bg-surface-card border border-border text-content text-sm hover:bg-surface-alt/50 transition-colors flex items-center gap-2"
          >
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
            </svg>
            {t('detail.gpx')}
          </button>
          <button
            onClick={handleEditAsRoute}
            disabled={isCreatingRoute || activity.status !== 'Completed'}
            className="px-3 py-2 rounded-lg bg-surface-card border border-accent text-accent text-sm hover:bg-cyan-900/30 disabled:opacity-40 disabled:cursor-not-allowed transition-colors flex items-center gap-2"
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

      {/* Key Stats */}
      {stats && (
        <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4">
          <div className="bg-surface-card rounded-xl p-4 border border-border">
            <p className="text-xs text-content-muted mb-1">{t('distance')}</p>
            <p className="text-lg font-bold text-accent">{stats.total_distance_km.toFixed(1)} {tc('unit.km')}</p>
          </div>
          <div className="bg-surface-card rounded-xl p-4 border border-border">
            <p className="text-xs text-content-muted mb-1">{t('detail.movingTime')}</p>
            <p className="text-lg font-bold text-content">{stats.moving_time.display}</p>
          </div>
          <div className="bg-surface-card rounded-xl p-4 border border-border">
            <p className="text-xs text-content-muted mb-1">{t('detail.avgSpeed')}</p>
            <p className="text-lg font-bold text-content">{stats.avg_moving_speed_kmh.toFixed(1)} {tc('unit.kmh')}</p>
          </div>
          <div className="bg-surface-card rounded-xl p-4 border border-border">
            <p className="text-xs text-content-muted mb-1">{t('detail.elevationGain')}</p>
            <p className="text-lg font-bold text-accent-green">+{Math.round(stats.elevation_gain_m)} {tc('unit.m')}</p>
          </div>
          <div className="bg-surface-card rounded-xl p-4 border border-border">
            <p className="text-xs text-content-muted mb-1">{t('detail.totalTime')}</p>
            <p className="text-lg font-bold text-content">{formatDuration(stats.total_time.seconds, tc)}</p>
          </div>
          <div className="bg-surface-card rounded-xl p-4 border border-border">
            <p className="text-xs text-content-muted mb-1">{t('detail.avgPace')}</p>
            <p className="text-lg font-bold text-content">{stats.avg_moving_pace}</p>
          </div>
          {activity.estimatedCalories != null && (
            <div className="bg-surface-card rounded-xl p-4 border border-border">
              <p className="text-xs text-content-muted mb-1">{t('enrichment.label.calories')}</p>
              <p className="text-lg font-bold text-orange-400">{Math.round(activity.estimatedCalories)} {tc('unit.kcal')}</p>
              {activity.calorieMethod && (
                <p className="text-xs text-content-muted/60 mt-0.5">{t(`enrichment.calorieMethod.${activity.calorieMethod}`)}</p>
              )}
            </div>
          )}
        </div>
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
          <h2 className="text-xl font-semibold text-content mb-4">{t('detail.performanceStats')}</h2>
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
          <div className="bg-surface-card rounded-xl p-4 border border-border">
            <p className="text-xs text-content-muted mb-1">{t('detail.maxSpeed')}</p>
            <p className="text-lg font-bold text-content">{stats.max_speed_kmh.toFixed(1)} {tc('unit.kmh')}</p>
          </div>
          <div className="bg-surface-card rounded-xl p-4 border border-border">
            <p className="text-xs text-content-muted mb-1">{t('detail.maxElevation')}</p>
            <p className="text-lg font-bold text-content">{Math.round(stats.max_elevation_m)} {tc('unit.m')}</p>
          </div>
          <div className="bg-surface-card rounded-xl p-4 border border-border">
            <p className="text-xs text-content-muted mb-1">{t('detail.minElevation')}</p>
            <p className="text-lg font-bold text-content">{Math.round(stats.min_elevation_m)} {tc('unit.m')}</p>
          </div>
          <div className="bg-surface-card rounded-xl p-4 border border-border">
            <p className="text-xs text-content-muted mb-1">{t('detail.stoppedTime')}</p>
            <p className="text-lg font-bold text-content">{formatDuration(stats.stopped_time.seconds, tc)}</p>
          </div>
          <div className="bg-surface-card rounded-xl p-4 border border-border">
            <p className="text-xs text-content-muted mb-1">{t('detail.stops')}</p>
            <p className="text-lg font-bold text-content">{stats.stop_count}</p>
          </div>
          <div className="bg-surface-card rounded-xl p-4 border border-border">
            <p className="text-xs text-content-muted mb-1">{t('detail.pointsPerKm')}</p>
            <p className="text-lg font-bold text-content">{Math.round(stats.points_per_km)}</p>
          </div>
          {stats.power && (
            <div className="bg-surface-card rounded-xl p-4 border border-border">
              <p className="text-xs text-content-muted mb-1">{t('detail.avgPower')}</p>
              <p className="text-lg font-bold text-yellow-400">{Math.round(stats.power.avg_watts)} {tc('unit.watts')}</p>
            </div>
          )}
          {stats.cadence && (() => {
            const isFootActivity = ['run', 'trail', 'hike', 'walk'].includes(activity.activityType);
            const cadenceValue = isFootActivity ? stats.cadence!.avg_rpm * 2 : stats.cadence!.avg_rpm;
            const cadenceUnit = isFootActivity ? tc('unit.spm') : tc('unit.rpm');
            return (
              <div className="bg-surface-card rounded-xl p-4 border border-border">
                <p className="text-xs text-content-muted mb-1">{t('detail.avgCadence')}</p>
                <p className="text-lg font-bold text-blue-400">{Math.round(cadenceValue)} {cadenceUnit}</p>
              </div>
            );
          })()}
        </div>
      )}

      {/* Activity Enrichment */}
      {activity.status === 'Completed' && (
        <div className="bg-surface-card rounded-2xl p-6 border border-border space-y-6">
          <div className="flex items-center justify-between">
            <h2 className="text-lg font-semibold text-content">{t('enrichment.title')}</h2>
            {enrichSaving && <span className="text-xs text-content-muted animate-pulse">{t('enrichment.saving')}</span>}
            {enrichSaved && !enrichSaving && <span className="text-xs text-green-400">{t('enrichment.saved')}</span>}
          </div>

          {/* Description */}
          <div>
            <label className="block text-xs text-content-muted mb-1.5">{t('enrichment.label.description')}</label>
            <textarea
              value={localDesc}
              onChange={(e) => setLocalDesc(e.target.value)}
              onBlur={() => saveEnrichment({ description: localDesc })}
              rows={3}
              placeholder={t('enrichment.placeholder.description')}
              className="w-full bg-surface-alt border border-border rounded-lg px-3 py-2 text-sm text-content placeholder-content-muted resize-none focus:outline-none focus:ring-1 focus:ring-accent/50"
            />
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-6">
            {/* Session Type */}
            <div>
              <label className="block text-xs text-content-muted mb-1.5">{t('enrichment.label.sessionType')}</label>
              <select
                value={localSessionType}
                onChange={(e) => {
                  setLocalSessionType(e.target.value);
                  saveEnrichment({ sessionType: e.target.value });
                }}
                className="w-full bg-surface-alt border border-border rounded-lg px-3 py-2 text-sm text-content focus:outline-none focus:ring-1 focus:ring-accent/50"
              >
                <option value="">{t('enrichment.sessionTypeNone')}</option>
                {(['long_run', 'race', 'training', 'recovery', 'intervals', 'tempo', 'easy'] as const).map((s) => (
                  <option key={s} value={s}>{tc(`sessionType.${s}`)}</option>
                ))}
              </select>
            </div>

            {/* RPE */}
            <div>
              <label className="block text-xs text-content-muted mb-1.5">
                {t('enrichment.label.perceivedExertion')}
                {localRpe && <span className="ml-2 text-accent">— {t(`enrichment.rpe.${localRpe}`)}</span>}
              </label>
              <div className="flex gap-1">
                {[1,2,3,4,5,6,7,8,9,10].map((n) => (
                  <button
                    key={n}
                    onClick={() => {
                      const newVal = localRpe === n ? null : n;
                      setLocalRpe(newVal);
                      saveEnrichment({ perceivedExertion: newVal ?? 0 });
                    }}
                    className="flex-1 py-1.5 rounded text-xs font-bold transition-colors"
                    style={{
                      backgroundColor: localRpe === n ? `hsl(${120 - (n - 1) * 12}, 70%, 35%)` : 'var(--surface-alt)',
                      color: localRpe === n ? '#fff' : localRpe && n <= localRpe ? `hsl(${120 - (n - 1) * 12}, 60%, 60%)` : 'var(--content-muted)',
                      borderWidth: 1,
                      borderColor: localRpe && n <= localRpe ? `hsl(${120 - (n - 1) * 12}, 50%, 40%)` : 'var(--border)',
                    }}
                  >
                    {n}
                  </button>
                ))}
              </div>
            </div>
          </div>

          {/* Tags */}
          <div>
            <label className="block text-xs text-content-muted mb-1.5">{t('enrichment.label.tags')}</label>
            <div className="flex flex-wrap gap-1.5 mb-2">
              {localTags.map((tag) => (
                <span
                  key={tag}
                  className="flex items-center gap-1 text-xs px-2 py-0.5 rounded-full bg-accent/15 text-accent border border-accent/30"
                >
                  {tag}
                  <button
                    onClick={() => {
                      const newTags = localTags.filter((t) => t !== tag);
                      setLocalTags(newTags);
                      saveEnrichment({ tags: newTags });
                    }}
                    className="hover:text-red-400 transition-colors leading-none"
                  >
                    ×
                  </button>
                </span>
              ))}
            </div>
            <input
              type="text"
              value={tagInput}
              onChange={(e) => setTagInput(e.target.value)}
              onKeyDown={(e) => {
                if ((e.key === 'Enter' || e.key === ',') && tagInput.trim()) {
                  e.preventDefault();
                  const newTag = tagInput.trim().toLowerCase();
                  if (!localTags.includes(newTag)) {
                    const newTags = [...localTags, newTag];
                    setLocalTags(newTags);
                    saveEnrichment({ tags: newTags });
                  }
                  setTagInput('');
                } else if (e.key === 'Backspace' && !tagInput && localTags.length > 0) {
                  const newTags = localTags.slice(0, -1);
                  setLocalTags(newTags);
                  saveEnrichment({ tags: newTags });
                }
              }}
              list="tag-suggestions"
              placeholder={t('enrichment.placeholder.tags')}
              className="w-full bg-surface-alt border border-border rounded-lg px-3 py-2 text-sm text-content placeholder-content-muted focus:outline-none focus:ring-1 focus:ring-accent/50"
            />
            <datalist id="tag-suggestions">
              {tagSuggestions.filter((s) => !localTags.includes(s)).map((s) => (
                <option key={s} value={s} />
              ))}
            </datalist>
          </div>
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
          avgBpm={activity.stats?.heart_rate?.avg_bpm}
          maxBpm={activity.stats?.heart_rate?.max_bpm}
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
        <div className="bg-surface-card rounded-2xl p-8 border border-border text-center">
          <div className="animate-spin rounded-full h-10 w-10 border-t-2 border-b-2 border-purple-400 mx-auto mb-4" />
          <p className="text-content font-medium">
            {activity.status === 'Analyzing' ? t('detail.analyzingGpx') : t('detail.aiProcessing')}
          </p>
          <p className="text-content-muted text-sm mt-1">{t('detail.autoRefresh')}</p>
        </div>
      )}

      {/* Change type confirmation dialog */}
      {pendingType && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50" onClick={() => !isChangingType && setPendingType(null)}>
          <div className="bg-surface-card border border-border rounded-xl p-6 max-w-sm mx-4 shadow-2xl" onClick={(e) => e.stopPropagation()}>
            <h3 className="text-lg font-semibold text-content mb-3">{t('detail.changeTypeTitle')}</h3>
            <p className="text-sm text-content mb-5">
              {t('detail.changeTypeConfirm', { type: tc(`activityType.${pendingType}`) })}
            </p>
            <div className="flex justify-end gap-3">
              <button
                onClick={() => setPendingType(null)}
                disabled={isChangingType}
                className="px-4 py-2 text-sm text-content hover:text-content transition-colors rounded-lg disabled:opacity-50"
              >
                {tc('button.cancel')}
              </button>
              <button
                onClick={async () => {
                  setIsChangingType(true);
                  try {
                    await api.updateActivity(activity.id, { activityType: pendingType });
                    await api.reanalyzeActivity(activity.id);
                    queryClient.invalidateQueries({ queryKey: ['activity', id] });
                  } finally {
                    setIsChangingType(false);
                    setPendingType(null);
                  }
                }}
                disabled={isChangingType}
                className="px-4 py-2 text-sm font-medium text-white bg-blue-600 hover:bg-blue-500 disabled:bg-blue-600/50 rounded-lg transition-colors flex items-center gap-2"
              >
                {isChangingType && <div className="animate-spin rounded-full h-3.5 w-3.5 border-2 border-white border-t-transparent" />}
                {tc('button.confirm')}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
