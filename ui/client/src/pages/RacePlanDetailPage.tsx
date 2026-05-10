import { useMemo, useState } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  ArrowLeft, Share2, Printer, Calculator, Calendar, Clock,
  RefreshCw, Save
} from 'lucide-react';
import { useRacePlan, useUpdateRacePlan, useComputeTimes } from '../hooks/useRacePlans';
import { useRacePlanStore } from '../stores/racePlanStore';
import { computeDayNightSegments } from '../utils/dayNight';
import { formatPageDuration, formatDate } from '../utils/format';
import CheckpointTimeline from '../components/race-plan/CheckpointTimeline';
import CheckpointEditor from '../components/race-plan/CheckpointEditor';
import ElevationWithCheckpoints from '../components/race-plan/ElevationWithCheckpoints';
import RacePlanMap from '../components/race-plan/RacePlanMap';
import PerformanceCoefficientSlider from '../components/race-plan/PerformanceCoefficientSlider';
import RacePlanShareModal from '../components/race-plan/RacePlanShareModal';
import NutritionPlanner from '../components/race-plan/NutritionPlanner';
import EquipmentChecklist from '../components/race-plan/EquipmentChecklist';
import type { RacePlanDetail, RacePlanUpdateRequest } from '../types/race-plan';

const STATUS_STYLE: Record<string, string> = {
  draft: 'bg-amber-500/15 text-amber-400',
  ready: 'bg-green-500/15 text-green-400',
  archived: 'bg-content-muted/20 text-content-muted',
};

export default function RacePlanDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { t } = useTranslation('race-plans');
  const { t: tc } = useTranslation();
  const { i18n } = useTranslation();

  const { data: plan, isLoading, error } = useRacePlan(id!);
  const updatePlan = useUpdateRacePlan();
  const computeTimes = useComputeTimes();
  const {
    activeTab, setActiveTab,
    showCheckpointEditor,
    showShareModal, openShareModal, closeShareModal,
  } = useRacePlanStore();

  // Local coefficient state (immediately updates slider, debounced save)
  const [localCoeff, setLocalCoeff] = useState<number | null>(null);
  const displayCoeff = localCoeff ?? plan?.performanceCoefficient ?? 0.75;

  // Day/night computation
  const dayNightSegments = useMemo(() => {
    if (!plan?.profile || !plan.raceDate || !plan.startTime) return undefined;
    if (plan.startLatitude == null || plan.startLongitude == null) return undefined;
    const checkpointAnchors = plan.checkpoints.map((cp) => ({
      distanceKm: cp.distanceKm,
      targetArrivalSeconds: cp.targetArrivalSeconds,
    }));
    return computeDayNightSegments(
      plan.profile,
      checkpointAnchors,
      new Date(plan.raceDate),
      plan.startTime,
      plan.startLatitude,
      plan.startLongitude,
    );
  }, [plan]);

  async function handleCoeffChange(coeff: number) {
    setLocalCoeff(coeff);
  }

  async function handleComputeTimes() {
    if (!plan) return;
    // Save coeff first if changed
    if (localCoeff != null && Math.abs(localCoeff - plan.performanceCoefficient) > 0.001) {
      const req: RacePlanUpdateRequest = {
        name: plan.name,
        activityType: plan.activityType,
        status: plan.status,
        performanceCoefficient: localCoeff,
      };
      await updatePlan.mutateAsync({ id: plan.id, data: req });
    }
    await computeTimes.mutateAsync(plan.id);
    setLocalCoeff(null);
  }

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-10 w-10 border-t-2 border-b-2 border-cyan-400" />
      </div>
    );
  }

  if (error || !plan) {
    return (
      <div className="bg-red-900/20 border border-red-800 rounded-xl p-6">
        <p className="text-red-400">{error?.message ?? 'Plan not found'}</p>
        <button onClick={() => navigate('/race-plans')} className="mt-3 text-sm text-content-muted hover:text-content">
          ← {tc('button.back')}
        </button>
      </div>
    );
  }

  const tabs = [
    { key: 'timeline', label: t('tabs.timeline') },
    { key: 'nutrition', label: t('tabs.nutrition') },
    { key: 'equipment', label: t('tabs.equipment') },
  ] as const;

  return (
    <div className="space-y-4">
      {/* Breadcrumb + actions */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
        <div className="flex items-center gap-3 min-w-0">
          <button
            onClick={() => navigate('/race-plans')}
            className="p-1.5 rounded-lg text-content-muted hover:text-content hover:bg-surface-alt/50 transition-colors shrink-0"
          >
            <ArrowLeft size={18} />
          </button>
          <div className="min-w-0">
            <h1 className="text-xl font-bold text-content truncate">{plan.name}</h1>
            <div className="flex items-center gap-2 mt-0.5 text-xs text-content-muted">
              {plan.raceDate && (
                <>
                  <Calendar size={12} />
                  <span>{formatDate(plan.raceDate, i18n.language)}</span>
                </>
              )}
              {plan.startTime && (
                <>
                  <Clock size={12} />
                  <span>{plan.startTime}</span>
                </>
              )}
              <span
                className={`px-1.5 py-0.5 rounded text-xs font-medium ${STATUS_STYLE[plan.status] ?? ''}`}
              >
                {t(`status.${plan.status}`)}
              </span>
            </div>
          </div>
        </div>

        <div className="flex items-center gap-2 shrink-0">
          {/* Compute times */}
          <button
            onClick={handleComputeTimes}
            disabled={computeTimes.isPending || updatePlan.isPending}
            className="flex items-center gap-1.5 px-3 py-2 rounded-lg bg-surface-card border border-border text-content-muted hover:text-content hover:bg-surface-alt/50 text-sm transition-colors disabled:opacity-50"
          >
            {computeTimes.isPending ? <RefreshCw size={15} className="animate-spin" /> : <Calculator size={15} />}
            {t('timing.computeTimes')}
          </button>

          {/* Share */}
          <button
            onClick={openShareModal}
            className="flex items-center gap-1.5 px-3 py-2 rounded-lg bg-surface-card border border-border text-content-muted hover:text-content hover:bg-surface-alt/50 text-sm transition-colors"
          >
            <Share2 size={15} />
            {t('share.title')}
          </button>

          {/* Print */}
          <Link
            to={`/race-plans/${plan.id}/print`}
            target="_blank"
            className="flex items-center gap-1.5 px-3 py-2 rounded-lg bg-surface-card border border-border text-content-muted hover:text-content hover:bg-surface-alt/50 text-sm transition-colors"
          >
            <Printer size={15} />
            {t('print.print')}
          </Link>
        </div>
      </div>

      {/* Stats bar */}
      <div className="grid grid-cols-2 sm:grid-cols-4 xl:grid-cols-6 gap-2">
        <StatBadge label={t('stats.distance')} value={`${plan.distanceKm.toFixed(1)} km`} />
        <StatBadge label={t('stats.elevGain')} value={`+${Math.round(plan.elevationGainM)} m`} color="text-cyan-400" />
        <StatBadge label={t('stats.elevLoss')} value={`-${Math.round(plan.elevationLossM)} m`} color="text-amber-400" />
        {plan.targetTimeSeconds && (
          <StatBadge label={t('stats.objectiveA')} value={formatPageDuration(plan.targetTimeSeconds, tc)} color="text-green-400" />
        )}
        {plan.targetTimeBSeconds && (
          <StatBadge label={t('stats.objectiveB')} value={formatPageDuration(plan.targetTimeBSeconds, tc)} />
        )}
        {plan.targetTimeCSeconds && (
          <StatBadge label={t('stats.objectiveC')} value={formatPageDuration(plan.targetTimeCSeconds, tc)} />
        )}
      </div>

      {/* Map + right panel */}
      <div className="grid grid-cols-1 xl:grid-cols-3 gap-4">
        {/* Map */}
        <div className="xl:col-span-2 h-64 rounded-xl overflow-hidden bg-surface-card border border-border">
          {plan.points && plan.points.length > 0 ? (
            <RacePlanMap plan={plan} />
          ) : (
            <div className="h-full flex items-center justify-center text-content-muted/40 text-sm">
              No track data
            </div>
          )}
        </div>

        {/* Right panel: coefficient + target times */}
        <div className="space-y-3">
          <PerformanceCoefficientSlider
            value={displayCoeff}
            onChange={handleCoeffChange}
            disabled={computeTimes.isPending}
          />

          {/* Edit plan meta */}
          <PlanMetaForm plan={plan} />
        </div>
      </div>

      {/* Elevation profile */}
      <div className="bg-surface-card border border-border rounded-xl p-4">
        <ElevationWithCheckpoints plan={plan} dayNightSegments={dayNightSegments} />
      </div>

      {/* Tabs */}
      <div className="bg-surface-card border border-border rounded-xl overflow-hidden">
        <div className="flex border-b border-border">
          {tabs.map(({ key, label }) => (
            <button
              key={key}
              onClick={() => setActiveTab(key)}
              className={`flex-1 py-3 text-sm font-medium transition-colors border-b-2 -mb-px ${
                activeTab === key
                  ? 'border-accent text-accent'
                  : 'border-transparent text-content-muted hover:text-content'
              }`}
            >
              {label}
            </button>
          ))}
        </div>
        <div className="p-4">
          {activeTab === 'timeline' && <CheckpointTimeline plan={plan} />}
          {activeTab === 'nutrition' && <NutritionPlanner plan={plan} />}
          {activeTab === 'equipment' && <EquipmentChecklist plan={plan} />}
        </div>
      </div>

      {/* Checkpoint editor slide-over */}
      {showCheckpointEditor && <CheckpointEditor plan={plan} />}

      {/* Share modal */}
      {showShareModal && <RacePlanShareModal plan={plan} onClose={closeShareModal} />}
    </div>
  );
}

function StatBadge({ label, value, color }: { label: string; value: string; color?: string }) {
  return (
    <div className="bg-surface-card border border-border rounded-xl p-3 text-center">
      <p className="text-xs text-content-muted">{label}</p>
      <p className={`text-sm font-bold mt-0.5 ${color ?? 'text-content'}`}>{value}</p>
    </div>
  );
}

function PlanMetaForm({ plan }: { plan: RacePlanDetail }) {
  const { t } = useTranslation('race-plans');
  const { t: tc } = useTranslation();
  const updatePlan = useUpdateRacePlan();
  const [editing, setEditing] = useState(false);
  const [form, setForm] = useState({
    raceDate: plan.raceDate ?? '',
    startTime: plan.startTime ?? '',
    targetTimeSeconds: plan.targetTimeSeconds ?? null,
    status: plan.status,
  });

  if (!editing) {
    return (
      <button
        onClick={() => setEditing(true)}
        className="w-full py-2 text-xs text-content-muted hover:text-content border border-dashed border-border rounded-lg transition-colors"
      >
        {t('timing.raceDate')} / {t('timing.startTime')} / {t('stats.targetTime')}
      </button>
    );
  }

  async function handleSave() {
    await updatePlan.mutateAsync({
      id: plan!.id,
      data: {
        name: plan!.name,
        activityType: plan!.activityType,
        status: form.status,
        performanceCoefficient: plan!.performanceCoefficient,
        raceDate: form.raceDate || null,
        startTime: form.startTime || null,
        targetTimeSeconds: form.targetTimeSeconds,
      },
    });
    setEditing(false);
  }

  return (
    <div className="bg-surface-card border border-border rounded-xl p-3 space-y-2">
      <div className="grid grid-cols-2 gap-2">
        <div>
          <label className="text-xs text-content-muted">{t('timing.raceDate')}</label>
          <input
            type="date"
            value={form.raceDate}
            onChange={(e) => setForm((f) => ({ ...f, raceDate: e.target.value }))}
            className="w-full mt-1 bg-surface-alt border border-border rounded-lg px-2 py-1.5 text-xs text-content focus:outline-none focus:ring-2 focus:ring-cyan-500"
          />
        </div>
        <div>
          <label className="text-xs text-content-muted">{t('timing.startTime')}</label>
          <input
            type="time"
            value={form.startTime}
            onChange={(e) => setForm((f) => ({ ...f, startTime: e.target.value }))}
            className="w-full mt-1 bg-surface-alt border border-border rounded-lg px-2 py-1.5 text-xs text-content focus:outline-none focus:ring-2 focus:ring-cyan-500"
          />
        </div>
      </div>
      <div>
        <label className="text-xs text-content-muted">{t('stats.objectiveA')} (min)</label>
        <input
          type="number"
          min={0}
          value={form.targetTimeSeconds != null ? Math.round(form.targetTimeSeconds / 60) : ''}
          onChange={(e) => {
            const v = parseInt(e.target.value, 10);
            setForm((f) => ({ ...f, targetTimeSeconds: isNaN(v) ? null : v * 60 }));
          }}
          className="w-full mt-1 bg-surface-alt border border-border rounded-lg px-2 py-1.5 text-xs text-content focus:outline-none focus:ring-2 focus:ring-cyan-500"
        />
      </div>
      <div>
        <label className="text-xs text-content-muted">{t('status.draft')}/{t('status.ready')}/{t('status.archived')}</label>
        <select
          value={form.status}
          onChange={(e) => setForm((f) => ({ ...f, status: e.target.value as 'draft' | 'ready' | 'archived' }))}
          className="w-full mt-1 bg-surface-alt border border-border rounded-lg px-2 py-1.5 text-xs text-content focus:outline-none focus:ring-2 focus:ring-cyan-500"
        >
          {['draft', 'ready', 'archived'].map((s) => (
            <option key={s} value={s}>{t(`status.${s}`)}</option>
          ))}
        </select>
      </div>
      <div className="flex gap-2 pt-1">
        <button
          onClick={() => setEditing(false)}
          className="flex-1 py-1.5 text-xs text-content-muted border border-border rounded-lg hover:text-content transition-colors"
        >
          {tc('button.cancel')}
        </button>
        <button
          onClick={handleSave}
          disabled={updatePlan.isPending}
          className="flex-1 py-1.5 text-xs bg-cyan-600 hover:bg-cyan-500 text-white rounded-lg font-medium transition-colors disabled:opacity-50 flex items-center justify-center gap-1"
        >
          <Save size={11} />
          {tc('button.save')}
        </button>
      </div>
    </div>
  );
}
