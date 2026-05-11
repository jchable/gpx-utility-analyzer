import { useState, useMemo, useEffect, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import { useSettings, useUpdateSettings, useIntegrations } from '../hooks/useActivities';
import { api } from '../api/client';
import type { AppSettings, IntegrationInfo } from '../types/activity';
import { BRAND_COLORS } from '../constants/brands';

function SectionCard({ title, id, children }: { title: string; id?: string; children: React.ReactNode }) {
  return (
    <div id={id} className="bg-surface-card rounded-xl border border-border p-6 scroll-mt-6">
      <h2 className="text-lg font-semibold text-content mb-4">{title}</h2>
      {children}
    </div>
  );
}

function FieldGroup({ label, children }: { label: React.ReactNode; children: React.ReactNode }) {
  return (
    <div className="mb-4 last:mb-0">
      <label className="block text-sm font-medium text-content-muted mb-1.5">{label}</label>
      {children}
    </div>
  );
}

function SelectInput({
  value,
  onChange,
  options,
}: {
  value: string;
  onChange: (v: string) => void;
  options: { value: string; label: string }[];
}) {
  return (
    <select
      value={value}
      onChange={(e) => onChange(e.target.value)}
      className="w-full bg-surface-input border border-border rounded-lg px-3 py-2 text-content text-sm focus:outline-none focus:border-blue-500/50"
    >
      {options.map((opt) => (
        <option key={opt.value} value={opt.value}>
          {opt.label}
        </option>
      ))}
    </select>
  );
}

// ── Integration connect/disconnect ───────────────────────────────────────────

const PROVIDER_STYLE: Record<string, { color: string; icon: string }> = {
  strava: { color: BRAND_COLORS.strava, icon: 'S' },
  garmin: { color: BRAND_COLORS.garmin, icon: 'G' },
};

function IntegrationCard({
  integration,
  onConnect,
  onDisconnect,
}: {
  integration: IntegrationInfo;
  onConnect: () => void;
  onDisconnect: () => void;
}) {
  const [loading, setLoading] = useState(false);
  const { t: ti } = useTranslation('integrations');
  const { t: ts } = useTranslation('settings');
  const { i18n } = useTranslation();

  const style = PROVIDER_STYLE[integration.provider] ?? {
    color: '#888888',
    icon: integration.provider.charAt(0).toUpperCase(),
  };

  const providerName = ti(`provider.${integration.provider}.name`, { defaultValue: integration.provider });

  const handleAction = async () => {
    setLoading(true);
    try {
      if (integration.isConnected) {
        await onDisconnect();
      } else {
        await onConnect();
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="rounded-xl border border-border bg-surface-alt/30 p-4">
      <div className="flex items-center gap-3 mb-2">
        <div
          className="w-8 h-8 rounded-lg flex items-center justify-center text-sm font-bold shrink-0"
          style={{ backgroundColor: style.color + '33', color: style.color }}
        >
          {style.icon}
        </div>
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <span className="text-sm font-semibold text-content">{providerName}</span>
            {integration.isConnected && (
              <span className="text-xs font-medium px-1.5 py-0.5 rounded-full bg-green-500/20 text-green-400">
                {ti('connected')}
              </span>
            )}
          </div>
          {integration.isConnected && integration.externalUserId && (
            <p className="text-xs text-content-muted mt-0.5">
              {ti('account', { userId: integration.externalUserId })}
              {integration.connectedAt && (
                <> — {new Date(integration.connectedAt).toLocaleDateString(i18n.language)}</>
              )}
            </p>
          )}
          {!integration.isConnected && (
            <p className="text-xs text-content-muted mt-0.5">{ts('serviceConnectionHint')}</p>
          )}
        </div>
        <button
          onClick={handleAction}
          disabled={loading}
          className={`shrink-0 px-3 py-1.5 rounded-lg text-xs font-medium transition-colors disabled:opacity-50 ${
            integration.isConnected
              ? 'bg-red-600/20 border border-red-800 text-red-400 hover:bg-red-600/30'
              : 'text-white hover:opacity-90'
          }`}
          style={!integration.isConnected ? { backgroundColor: style.color } : undefined}
        >
          {loading ? ti('processing') : integration.isConnected ? ts('disconnect') : ts('connect')}
        </button>
      </div>
    </div>
  );
}

// ── Main page ────────────────────────────────────────────────────────────────

export default function SettingsPage() {
  const { t } = useTranslation('settings');
  const { data: settings, isLoading } = useSettings();
  const updateMutation = useUpdateSettings();
  const { data: integrations, refetch: refetchIntegrations } = useIntegrations();

  const baseForm = useMemo((): AppSettings | null => {
    if (!settings) return null;
    return { ...settings, analysis: { ...settings.analysis } };
  }, [settings]);

  const [formEdits, setFormEdits] = useState<AppSettings | null>(null);
  const form = formEdits ?? baseForm;
  const [saved, setSaved] = useState(false);

  const setForm = useCallback(
    (updater: (prev: AppSettings | null) => AppSettings | null) => {
      setFormEdits((prev) => updater(prev ?? baseForm));
    },
    [baseForm],
  );

  const scrollToHash = useCallback(() => {
    const hash = window.location.hash;
    if (hash) {
      const el = document.querySelector(hash);
      if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
  }, []);

  useEffect(() => {
    if (form) {
      const timer = setTimeout(scrollToHash, 100);
      return () => clearTimeout(timer);
    }
  }, [form, scrollToHash]);

  if (isLoading || !form) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-8 w-8 border-2 border-blue-500 border-t-transparent" />
      </div>
    );
  }

  const handleSave = async () => {
    setSaved(false);
    await updateMutation.mutateAsync(form);
    setSaved(true);
    setTimeout(() => setSaved(false), 3000);
  };

  const updateAnalysis = (field: string, value: string | boolean) => {
    setForm((f) => f ? { ...f, analysis: { ...f.analysis, [field]: value } } : f);
  };

  const handleConnect = async (provider: string) => {
    await api.connectIntegration(provider);
  };

  const handleDisconnect = async (provider: string) => {
    await api.disconnectIntegration(provider);
    refetchIntegrations();
  };

  const presetOptions = [
    { value: 'trail', label: t('preset.trail') },
    { value: 'hiking', label: t('preset.hiking') },
    { value: 'cycling', label: t('preset.cycling') },
    { value: 'running', label: t('preset.running') },
    { value: 'walking', label: t('preset.walking') },
    { value: 'swimming', label: t('preset.swimming') },
  ];

  const smoothingOptions = [
    { value: 'none', label: t('smoothingLevel.none') },
    { value: 'light', label: t('smoothingLevel.light') },
    { value: 'medium', label: t('smoothingLevel.medium') },
    { value: 'heavy', label: t('smoothingLevel.heavy') },
  ];

  const elevationAlgorithmOptions = [
    { value: 'threshold', label: t('elevAlgo.threshold') },
    { value: 'douglas-peucker', label: t('elevAlgo.douglas-peucker') },
    { value: 'segments', label: t('elevAlgo.segments') },
  ];

  return (
    <div>
      <h1 className="text-2xl font-bold text-content mb-6">{t('title')}</h1>

      <div className="space-y-6">
        {/* Integrations — connect/disconnect */}
        {integrations && integrations.length > 0 && (
          <SectionCard title={t('integrations')}>
            <p className="text-sm text-content-muted mb-4">{t('serviceConnectionHint')}</p>
            <div className="space-y-3">
              {integrations.map((integration) => (
                <IntegrationCard
                  key={integration.provider}
                  integration={integration}
                  onConnect={() => handleConnect(integration.provider)}
                  onDisconnect={() => handleDisconnect(integration.provider)}
                />
              ))}
            </div>
          </SectionCard>
        )}

        {/* Analysis Preferences */}
        <SectionCard title={t('analysisPreferences')}>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <FieldGroup label={t('defaultPreset')}>
              <SelectInput
                value={form.analysis.preset}
                onChange={(v) => updateAnalysis('preset', v)}
                options={presetOptions}
              />
            </FieldGroup>
            <FieldGroup label={t('smoothing')}>
              <SelectInput
                value={form.analysis.smoothing}
                onChange={(v) => updateAnalysis('smoothing', v)}
                options={smoothingOptions}
              />
            </FieldGroup>
            <FieldGroup label={t('trackSmoothing')}>
              <SelectInput
                value={form.analysis.trackSmoothing}
                onChange={(v) => updateAnalysis('trackSmoothing', v)}
                options={smoothingOptions}
              />
            </FieldGroup>
            <FieldGroup label={t('elevationAlgorithm')}>
              <SelectInput
                value={form.analysis.elevationAlgorithm}
                onChange={(v) => updateAnalysis('elevationAlgorithm', v)}
                options={elevationAlgorithmOptions}
              />
            </FieldGroup>
            <div className="col-span-full flex items-start gap-3 pt-2">
              <button
                type="button"
                role="switch"
                aria-checked={form.analysis.fixAnomalies}
                onClick={() => updateAnalysis('fixAnomalies', !form.analysis.fixAnomalies)}
                className={`relative inline-flex h-6 w-11 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 focus:ring-offset-surface-card ${form.analysis.fixAnomalies ? 'bg-blue-600' : 'bg-surface-alt'}`}
              >
                <span
                  className={`pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out ${form.analysis.fixAnomalies ? 'translate-x-5' : 'translate-x-0'}`}
                />
              </button>
              <div>
                <span className="text-sm font-medium text-content-muted">{t('fixAnomalies')}</span>
                <p className="text-xs text-content-muted mt-0.5">{t('fixAnomaliesHint')}</p>
              </div>
            </div>
            <div className="col-span-full flex items-start gap-3 pt-2">
              <button
                type="button"
                role="switch"
                aria-checked={form.analysis.autoDetectActivityType}
                onClick={() => updateAnalysis('autoDetectActivityType', !form.analysis.autoDetectActivityType)}
                className={`relative inline-flex h-6 w-11 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 focus:ring-offset-surface-card ${form.analysis.autoDetectActivityType ? 'bg-blue-600' : 'bg-surface-alt'}`}
              >
                <span
                  className={`pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out ${form.analysis.autoDetectActivityType ? 'translate-x-5' : 'translate-x-0'}`}
                />
              </button>
              <div>
                <span className="text-sm font-medium text-content-muted">{t('autoDetectActivityType')}</span>
                <p className="text-xs text-content-muted mt-0.5">{t('autoDetectHint')}</p>
              </div>
            </div>
          </div>
        </SectionCard>

        {/* Save button */}
        <div className="flex items-center gap-3">
          <button
            onClick={handleSave}
            disabled={updateMutation.isPending}
            className="px-6 py-2.5 bg-blue-600 hover:bg-blue-500 disabled:bg-blue-600/50 text-white font-medium rounded-lg transition-colors"
          >
            {updateMutation.isPending ? t('saving') : t('saveSettings')}
          </button>
          {saved && (
            <span className="text-emerald-400 text-sm">{t('savedSuccess')}</span>
          )}
          {updateMutation.isError && (
            <span className="text-red-400 text-sm">
              {t('saveFailed', { message: updateMutation.error.message })}
            </span>
          )}
        </div>
      </div>
    </div>
  );
}
