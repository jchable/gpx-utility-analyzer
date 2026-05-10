import { useState, useRef } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Flag, Upload, Plus, Trash2, Map, Calendar, TrendingUp, ChevronRight } from 'lucide-react';
import { useRacePlans, useDeleteRacePlan, useImportRacePlanGpx, useCreateRacePlanFromRoute } from '../hooks/useRacePlans';
import { useRoutes } from '../hooks/useRoutes';
import Modal from '../components/ui/Modal';
import { formatDate, formatPageDuration } from '../utils/format';
import { ACTIVITY_COLORS } from '../types/activity';

const STATUS_STYLE: Record<string, string> = {
  draft: 'bg-amber-500/15 text-amber-400',
  ready: 'bg-green-500/15 text-green-400',
  archived: 'bg-content-muted/20 text-content-muted',
};

export default function RacePlansPage() {
  const { t } = useTranslation('race-plans');
  const { t: tc } = useTranslation();
  const { i18n } = useTranslation();
  const navigate = useNavigate();
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [page, setPage] = useState(1);
  const [typeFilter] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [showRouteModal, setShowRouteModal] = useState(false);

  const { data: plans, isLoading, error } = useRacePlans(page, typeFilter || undefined, statusFilter || undefined);
  const deletePlan = useDeleteRacePlan();
  const importGpx = useImportRacePlanGpx();
  const createFromRoute = useCreateRacePlanFromRoute();
  const { data: routes } = useRoutes(1, undefined, 'published');

  async function handleImport(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    try {
      const plan = await importGpx.mutateAsync(file);
      navigate(`/race-plans/${plan.id}`);
    } catch { /* error handled by mutation */ }
    if (fileInputRef.current) fileInputRef.current.value = '';
  }

  async function handleCreateFromRoute(routeId: string) {
    setShowRouteModal(false);
    try {
      const plan = await createFromRoute.mutateAsync(routeId);
      navigate(`/race-plans/${plan.id}`);
    } catch { /* error handled */ }
  }

  async function handleDelete(e: React.MouseEvent, id: string) {
    e.preventDefault();
    e.stopPropagation();
    if (!confirm(t('confirmDelete'))) return;
    await deletePlan.mutateAsync(id);
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold text-content tracking-tight">{t('title')}</h1>
          <p className="text-content-muted mt-1">{t('subtitle')}</p>
        </div>
        <div className="flex items-center gap-3 flex-wrap">
          {/* Status filter */}
          <select
            value={statusFilter}
            onChange={(e) => { setStatusFilter(e.target.value); setPage(1); }}
            className="bg-surface-card text-content border border-border rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500 appearance-none cursor-pointer"
          >
            <option value="">{tc('button.all', { defaultValue: 'All' })}</option>
            <option value="draft">{t('status.draft')}</option>
            <option value="ready">{t('status.ready')}</option>
            <option value="archived">{t('status.archived')}</option>
          </select>

          {/* Import GPX */}
          <input ref={fileInputRef} type="file" accept=".gpx" className="hidden" onChange={handleImport} />
          <button
            onClick={() => fileInputRef.current?.click()}
            disabled={importGpx.isPending}
            className="bg-surface-card hover:bg-surface-alt/50 text-content px-4 py-2 rounded-lg text-sm font-medium transition-colors border border-border flex items-center gap-2"
          >
            <Upload size={15} />
            {importGpx.isPending ? '…' : t('importGpx')}
          </button>

          {/* Create from route */}
          <button
            onClick={() => setShowRouteModal(true)}
            disabled={createFromRoute.isPending}
            className="bg-surface-card hover:bg-surface-alt/50 text-content px-4 py-2 rounded-lg text-sm font-medium transition-colors border border-border flex items-center gap-2"
          >
            <Map size={15} />
            {t('fromRoute')}
          </button>

          {/* New blank plan (via GPX import) */}
          <button
            onClick={() => fileInputRef.current?.click()}
            className="bg-cyan-600 hover:bg-cyan-500 text-white px-4 py-2 rounded-lg text-sm font-medium transition-colors flex items-center gap-2"
          >
            <Plus size={15} />
            {t('newPlan')}
          </button>
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
          <p className="text-red-400">{error.message}</p>
        </div>
      )}

      {/* Empty state */}
      {plans && plans.length === 0 && (
        <div className="text-center py-20">
          <Flag className="w-16 h-16 mx-auto text-content-muted/40 mb-4" />
          <p className="text-content-muted text-lg font-medium">{t('noPlans')}</p>
          <p className="text-content-muted/60 mt-1 text-sm">{t('noPlansHint')}</p>
        </div>
      )}

      {/* Plans grid */}
      {plans && plans.length > 0 && (
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
          {plans.map((plan) => {
            const color = ACTIVITY_COLORS[plan.activityType] ?? ACTIVITY_COLORS.other;
            return (
              <Link
                key={plan.id}
                to={`/race-plans/${plan.id}`}
                className="bg-surface-card rounded-2xl p-5 border border-border hover:border-content-muted/30 transition-all hover:shadow-lg hover:shadow-black/20 group relative"
              >
                {/* Header */}
                <div className="flex items-start justify-between mb-3">
                  <div className="min-w-0 flex-1">
                    <h3 className="text-content font-semibold truncate group-hover:text-accent transition-colors">
                      {plan.name}
                    </h3>
                    <div className="flex items-center gap-2 mt-1 flex-wrap">
                      <span
                        className="text-xs font-bold px-2 py-0.5 rounded-full"
                        style={{ backgroundColor: color + '22', color }}
                      >
                        {t(`type.${plan.activityType}`, { defaultValue: plan.activityType })}
                      </span>
                      <span className={`text-xs font-medium px-2 py-0.5 rounded-full ${STATUS_STYLE[plan.status] ?? ''}`}>
                        {t(`status.${plan.status}`)}
                      </span>
                    </div>
                  </div>
                  <ChevronRight size={18} className="text-content-muted/40 group-hover:text-accent shrink-0 mt-0.5 transition-colors" />
                </div>

                {/* Race date */}
                {plan.raceDate && (
                  <div className="flex items-center gap-1.5 text-xs text-content-muted mb-3">
                    <Calendar size={13} />
                    <span>{formatDate(plan.raceDate, i18n.language)}</span>
                    {plan.startTime && <span>· {plan.startTime}</span>}
                  </div>
                )}

                {/* Stats row */}
                <div className="grid grid-cols-3 gap-2 mt-3">
                  <div className="bg-surface-alt/30 rounded-lg p-2 text-center">
                    <p className="text-xs text-content-muted">{t('stats.distance')}</p>
                    <p className="text-sm font-semibold text-content">
                      {plan.distanceKm.toFixed(1)} <span className="text-xs font-normal">km</span>
                    </p>
                  </div>
                  <div className="bg-surface-alt/30 rounded-lg p-2 text-center">
                    <p className="text-xs text-content-muted">{t('stats.elevGain')}</p>
                    <p className="text-sm font-semibold text-cyan-400">
                      +{Math.round(plan.elevationGainM)} <span className="text-xs font-normal">m</span>
                    </p>
                  </div>
                  <div className="bg-surface-alt/30 rounded-lg p-2 text-center">
                    <p className="text-xs text-content-muted">{t('stats.targetTime')}</p>
                    <p className="text-sm font-semibold text-content">
                      {plan.targetTimeSeconds ? formatPageDuration(plan.targetTimeSeconds, tc) : '—'}
                    </p>
                  </div>
                </div>

                {/* Checkpoints count */}
                <div className="flex items-center justify-between mt-3 pt-3 border-t border-border/50">
                  <div className="flex items-center gap-1.5 text-xs text-content-muted">
                    <TrendingUp size={13} />
                    <span>{plan.checkpointCount} {t('stats.checkpoints')}</span>
                  </div>
                  <p className="text-xs text-content-muted/60">{formatDate(plan.updatedAt, i18n.language)}</p>
                </div>

                {/* Delete button */}
                <button
                  onClick={(e) => handleDelete(e, plan.id)}
                  className="absolute top-3 right-8 opacity-0 group-hover:opacity-100 p-1.5 rounded-lg text-content-muted hover:text-red-400 hover:bg-red-900/20 transition-all"
                  title={tc('button.delete')}
                >
                  <Trash2 size={14} />
                </button>
              </Link>
            );
          })}
        </div>
      )}

      {/* Route picker modal */}
      {showRouteModal && (
        <Modal title={t('fromRoute')} onClose={() => setShowRouteModal(false)} maxWidth="max-w-lg">
          <div className="space-y-2 max-h-80 overflow-y-auto">
            {!routes || routes.length === 0 ? (
              <p className="text-content-muted text-sm text-center py-8">
                {tc('noData', { defaultValue: 'No routes available' })}
              </p>
            ) : (
              routes.map((route) => (
                <button
                  key={route.id}
                  onClick={() => handleCreateFromRoute(route.id)}
                  className="w-full text-left px-4 py-3 rounded-lg bg-surface-alt/30 hover:bg-surface-alt/60 transition-colors border border-transparent hover:border-border/50"
                >
                  <p className="text-content font-medium text-sm">{route.name}</p>
                  <p className="text-content-muted text-xs mt-0.5">
                    {route.distanceKm?.toFixed(1)} km · +{Math.round(route.elevationGainM ?? 0)} m
                  </p>
                </button>
              ))
            )}
          </div>
          <div className="flex justify-end mt-4">
            <button
              onClick={() => setShowRouteModal(false)}
              className="px-4 py-2 text-sm text-content-muted hover:text-content transition-colors"
            >
              {tc('button.cancel')}
            </button>
          </div>
        </Modal>
      )}
    </div>
  );
}
