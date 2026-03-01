import { useState, useRef } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useRoutes, useDeleteRoute, useImportRouteGpx } from '../hooks/useRoutes';
import { ACTIVITY_COLORS, ACTIVITY_TYPES } from '../types/activity';

export default function RoutesPage() {
  const { t } = useTranslation('routes');
  const { t: tc } = useTranslation();
  const { i18n } = useTranslation();
  const navigate = useNavigate();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [page, setPage] = useState(1);
  const [typeFilter, setTypeFilter] = useState('');
  const { data: routes, isLoading, error } = useRoutes(page, typeFilter || undefined);
  const deleteRoute = useDeleteRoute();
  const importGpx = useImportRouteGpx();

  function formatDuration(seconds: number): string {
    const h = Math.floor(seconds / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    if (h > 0) return tc('format.durationHM', { h, m });
    return tc('format.durationM', { m });
  }

  function formatDate(iso: string): string {
    return new Date(iso).toLocaleDateString(i18n.language, {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
    });
  }

  async function handleImport(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    try {
      const route = await importGpx.mutateAsync(file);
      navigate(`/editor/${route.id}`);
    } catch {
      // error handled by mutation
    }
    if (fileInputRef.current) fileInputRef.current.value = '';
  }

  async function handleDelete(e: React.MouseEvent, id: string) {
    e.preventDefault();
    e.stopPropagation();
    if (!confirm(t('confirmDelete'))) return;
    await deleteRoute.mutateAsync(id);
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold text-white tracking-tight">{t('title')}</h1>
          <p className="text-slate-400 mt-1">{t('subtitle')}</p>
        </div>
        <div className="flex items-center gap-3">
          <select
            value={typeFilter}
            onChange={(e) => {
              setTypeFilter(e.target.value);
              setPage(1);
            }}
            className="bg-[#16213e] text-slate-300 border border-slate-700 rounded-lg px-4 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500 focus:border-transparent appearance-none cursor-pointer"
          >
            <option value="">{t('allTypes')}</option>
            {ACTIVITY_TYPES.map((type) => (
              <option key={type} value={type}>
                {tc(`activityType.${type}`)}
              </option>
            ))}
          </select>
          <input
            ref={fileInputRef}
            type="file"
            accept=".gpx"
            className="hidden"
            onChange={handleImport}
          />
          <button
            onClick={() => fileInputRef.current?.click()}
            disabled={importGpx.isPending}
            className="bg-[#16213e] hover:bg-slate-700/50 text-slate-300 px-4 py-2.5 rounded-lg text-sm font-medium transition-colors border border-slate-700 flex items-center gap-2"
          >
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12" />
            </svg>
            {t('importGpx')}
          </button>
          <Link
            to="/editor"
            className="bg-cyan-600 hover:bg-cyan-500 text-white px-4 py-2.5 rounded-lg text-sm font-medium transition-colors flex items-center gap-2"
          >
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
            </svg>
            {t('newRoute')}
          </Link>
        </div>
      </div>

      {isLoading && (
        <div className="flex items-center justify-center h-64">
          <div className="animate-spin rounded-full h-10 w-10 border-t-2 border-b-2 border-cyan-400" />
        </div>
      )}

      {error && (
        <div className="bg-red-900/20 border border-red-800 rounded-xl p-4">
          <p className="text-red-400">{error.message}</p>
        </div>
      )}

      {routes && routes.length === 0 && (
        <div className="text-center py-16">
          <svg className="w-16 h-16 mx-auto text-slate-600 mb-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M9 20l-5.447-2.724A1 1 0 013 16.382V5.618a1 1 0 011.447-.894L9 7m0 13l6-3m-6 3V7m6 10l4.553 2.276A1 1 0 0021 18.382V7.618a1 1 0 00-.553-.894L15 4m0 13V4m0 0L9 7" />
          </svg>
          <p className="text-slate-400 text-lg">{t('empty')}</p>
        </div>
      )}

      {routes && routes.length > 0 && (
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
          {routes.map((route) => {
            const color = ACTIVITY_COLORS[route.activityType] || ACTIVITY_COLORS.other;
            const typeLabel = tc(`activityType.${route.activityType}`, { defaultValue: route.activityType });

            return (
              <Link
                key={route.id}
                to={`/editor/${route.id}`}
                className="bg-[#16213e] rounded-2xl p-5 border border-slate-700/50 hover:border-slate-600 transition-all hover:shadow-lg hover:shadow-black/20 group relative"
              >
                <div className="flex items-start justify-between mb-4">
                  <div className="min-w-0 flex-1">
                    <h3 className="text-white font-semibold truncate group-hover:text-cyan-300 transition-colors">
                      {route.name}
                    </h3>
                    <p className="text-xs text-slate-500 mt-1">{formatDate(route.updatedAt)}</p>
                  </div>
                  <div className="flex items-center gap-2 shrink-0 ml-3">
                    {route.status === 'draft' && (
                      <span className="text-xs font-medium px-2 py-0.5 rounded-full bg-amber-500/20 text-amber-400">
                        {t('status.draft')}
                      </span>
                    )}
                    <span
                      className="text-xs font-bold px-2.5 py-1 rounded-full"
                      style={{ backgroundColor: color + '22', color }}
                    >
                      {typeLabel}
                    </span>
                  </div>
                </div>

                <div className="grid grid-cols-3 gap-3">
                  <div>
                    <p className="text-xs text-slate-500 mb-0.5">{t('stats.distance')}</p>
                    <p className="text-sm font-semibold text-white">{route.distanceKm.toFixed(1)} {tc('unit.km')}</p>
                  </div>
                  <div>
                    <p className="text-xs text-slate-500 mb-0.5">{t('stats.elevationGain')}</p>
                    <p className="text-sm font-semibold text-white">{Math.round(route.elevationGainM)} {tc('unit.m')}</p>
                  </div>
                  <div>
                    <p className="text-xs text-slate-500 mb-0.5">{t('stats.estimatedTime')}</p>
                    <p className="text-sm font-semibold text-white">{formatDuration(route.estimatedTimeSeconds)}</p>
                  </div>
                </div>

                {route.tags && (
                  <div className="mt-3 flex flex-wrap gap-1">
                    {route.tags.split(',').map((tag) => (
                      <span key={tag.trim()} className="text-xs px-2 py-0.5 rounded-full bg-slate-700/50 text-slate-400">
                        {tag.trim()}
                      </span>
                    ))}
                  </div>
                )}

                <button
                  onClick={(e) => handleDelete(e, route.id)}
                  className="absolute top-3 right-3 opacity-0 group-hover:opacity-100 transition-opacity p-1.5 rounded-lg hover:bg-red-500/20 text-slate-500 hover:text-red-400"
                  title={tc('button.delete')}
                >
                  <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                  </svg>
                </button>
              </Link>
            );
          })}
        </div>
      )}

      {routes && routes.length > 0 && (
        <div className="flex items-center justify-center gap-3 pt-4">
          <button
            onClick={() => setPage((p) => Math.max(1, p - 1))}
            disabled={page === 1}
            className="px-4 py-2 rounded-lg bg-[#16213e] border border-slate-700 text-slate-300 text-sm disabled:opacity-40 disabled:cursor-not-allowed hover:bg-slate-700/50 transition-colors"
          >
            {tc('button.previous')}
          </button>
          <span className="text-slate-400 text-sm">{tc('format.page', { page })}</span>
          <button
            onClick={() => setPage((p) => p + 1)}
            disabled={routes.length < 20}
            className="px-4 py-2 rounded-lg bg-[#16213e] border border-slate-700 text-slate-300 text-sm disabled:opacity-40 disabled:cursor-not-allowed hover:bg-slate-700/50 transition-colors"
          >
            {tc('button.next')}
          </button>
        </div>
      )}
    </div>
  );
}
