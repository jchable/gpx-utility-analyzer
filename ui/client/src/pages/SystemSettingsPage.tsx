import { useState, useMemo, useCallback, useEffect } from 'react';
import { Navigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../contexts/AuthContext';
import { useGlobalSettings, useUpdateGlobalSettings } from '../hooks/useActivities';
import type { GlobalAppSettings } from '../types/activity';
import { BRAND_COLORS } from '../constants/brands';

function SectionCard({ title, description, id, children }: { title: string; description?: string; id?: string; children: React.ReactNode }) {
  return (
    <div id={id} className="bg-surface-card rounded-xl border border-border p-6 scroll-mt-6">
      <h2 className="text-lg font-semibold text-content mb-1">{title}</h2>
      {description && <p className="text-sm text-content-muted mb-4">{description}</p>}
      {!description && <div className="mb-4" />}
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

function TextInput({
  value,
  onChange,
  placeholder,
  type = 'text',
}: {
  value: string;
  onChange: (v: string) => void;
  placeholder?: string;
  type?: 'text' | 'password' | 'number';
}) {
  return (
    <input
      type={type}
      value={value}
      onChange={(e) => onChange(e.target.value)}
      placeholder={placeholder}
      className="w-full bg-surface-input border border-border rounded-lg px-3 py-2 text-content text-sm focus:outline-none focus:border-blue-500/50 placeholder-content-muted/60"
    />
  );
}

function ConfiguredBadge({ label }: { label: string }) {
  if (!label) return null;
  return (
    <span className="ml-2 inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-emerald-500/20 text-emerald-400">
      {label}
    </span>
  );
}

function ProviderBadge({ color, letter, name }: { color: string; letter: string; name: string }) {
  return (
    <span className="inline-flex items-center gap-1.5 text-sm font-semibold text-content">
      <span
        className="inline-flex items-center justify-center w-5 h-5 rounded text-xs font-bold"
        style={{ backgroundColor: color + '33', color }}
      >
        {letter}
      </span>
      {name}
    </span>
  );
}

export default function SystemSettingsPage() {
  const { isAdmin } = useAuth();
  const { t } = useTranslation('system-settings');
  const { data: globalSettings, isLoading, isError } = useGlobalSettings();
  const updateMutation = useUpdateGlobalSettings();

  const baseForm = useMemo((): GlobalAppSettings | null => {
    if (!globalSettings) return null;
    return {
      aiProvider: { ...globalSettings.aiProvider, apiKey: '' },
      integrations: {
        strava: { ...globalSettings.integrations.strava, clientSecret: '' },
        garmin: { ...globalSettings.integrations.garmin, consumerSecret: '' },
      },
    };
  }, [globalSettings]);

  const [formEdits, setFormEdits] = useState<GlobalAppSettings | null>(null);
  const form = formEdits ?? baseForm;
  const [saved, setSaved] = useState(false);

  const setForm = useCallback(
    (updater: (prev: GlobalAppSettings | null) => GlobalAppSettings | null) => {
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

  // Redirect non-admin users
  if (!isAdmin) return <Navigate to="/settings" replace />;

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-8 w-8 border-2 border-blue-500 border-t-transparent" />
      </div>
    );
  }

  if (isError || !form) {
    return (
      <div className="flex items-center justify-center h-64">
        <p className="text-red-400">{t('loadError', { defaultValue: 'Failed to load system settings.' })}</p>
      </div>
    );
  }

  const handleSave = async () => {
    setSaved(false);
    await updateMutation.mutateAsync(form);
    setSaved(true);
    setTimeout(() => setSaved(false), 3000);
  };

  const updateAiProvider = (field: string, value: string) => {
    setForm((f) => f ? { ...f, aiProvider: { ...f.aiProvider, [field]: value } } : f);
  };

  const updateStrava = (field: string, value: string) => {
    setForm((f) =>
      f ? { ...f, integrations: { ...f.integrations, strava: { ...f.integrations.strava, [field]: value } } } : f,
    );
  };

  const updateGarmin = (field: string, value: string) => {
    setForm((f) =>
      f ? { ...f, integrations: { ...f.integrations, garmin: { ...f.integrations.garmin, [field]: value } } } : f,
    );
  };

  const providerOptions = (globalSettings?.aiProvider.availableProviders ?? []).map((p) => ({
    value: p,
    label: p.charAt(0).toUpperCase() + p.slice(1),
  }));

  return (
    <div>
      <h1 className="text-2xl font-bold text-content mb-2">{t('title')}</h1>
      <p className="text-sm text-content-muted mb-6">{t('subtitle')}</p>

      <div className="space-y-6">
        {/* Integration Credentials */}
        <SectionCard title={t('integrationCredentials')} description={t('integrationCredentialsHint')}>
          <div className="space-y-6">
            {/* Strava */}
            <div>
              <div className="mb-3">
                <ProviderBadge color={BRAND_COLORS.strava} letter="S" name="Strava" />
                <p className="text-xs text-content-muted mt-1">{t('stravaHint')}</p>
              </div>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <FieldGroup label={t('clientId')}>
                  <TextInput
                    value={form.integrations.strava.clientId}
                    onChange={(v) => updateStrava('clientId', v)}
                    placeholder={t('placeholder.stravaClientId')}
                  />
                </FieldGroup>
                <FieldGroup
                  label={
                    <>
                      {t('clientSecret')}
                      {(globalSettings?.integrations.strava.hasClientSecret ?? false) && (
                        <ConfiguredBadge label={t('configured')} />
                      )}
                    </>
                  }
                >
                  <TextInput
                    value={form.integrations.strava.clientSecret}
                    onChange={(v) => updateStrava('clientSecret', v)}
                    placeholder={t('placeholder.newSecret')}
                    type="password"
                  />
                </FieldGroup>
              </div>
            </div>

            <div className="border-t border-border pt-5">
              {/* Garmin */}
              <div className="mb-3">
                <ProviderBadge color={BRAND_COLORS.garmin} letter="G" name="Garmin Connect" />
                <p className="text-xs text-content-muted mt-1">{t('garminHint')}</p>
              </div>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <FieldGroup label={t('consumerKey')}>
                  <TextInput
                    value={form.integrations.garmin.consumerKey}
                    onChange={(v) => updateGarmin('consumerKey', v)}
                    placeholder={t('placeholder.garminConsumerKey')}
                  />
                </FieldGroup>
                <FieldGroup
                  label={
                    <>
                      {t('consumerSecret')}
                      {(globalSettings?.integrations.garmin.hasConsumerSecret ?? false) && (
                        <ConfiguredBadge label={t('configured')} />
                      )}
                    </>
                  }
                >
                  <TextInput
                    value={form.integrations.garmin.consumerSecret}
                    onChange={(v) => updateGarmin('consumerSecret', v)}
                    placeholder={t('placeholder.newSecret')}
                    type="password"
                  />
                </FieldGroup>
              </div>
            </div>
          </div>
        </SectionCard>

        {/* AI Provider */}
        <SectionCard id="ai-provider" title={t('aiProvider')} description={t('aiProviderHint')}>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <FieldGroup label={t('provider')}>
              <SelectInput
                value={form.aiProvider.name}
                onChange={(v) => updateAiProvider('name', v)}
                options={[{ value: '', label: t('selectProvider') }, ...providerOptions]}
              />
            </FieldGroup>
            <FieldGroup label={t('model')}>
              <TextInput
                value={form.aiProvider.model}
                onChange={(v) => updateAiProvider('model', v)}
                placeholder="e.g. gemini-2.5-flash"
              />
            </FieldGroup>
            <FieldGroup
              label={
                <>
                  {t('apiKey')}
                  {(globalSettings?.aiProvider.hasApiKey ?? false) && (
                    <ConfiguredBadge label={t('configured')} />
                  )}
                </>
              }
            >
              <TextInput
                value={form.aiProvider.apiKey}
                onChange={(v) => updateAiProvider('apiKey', v)}
                placeholder={t('placeholder.newKey')}
                type="password"
              />
            </FieldGroup>
            <FieldGroup label={t('endpointOptional')}>
              <TextInput
                value={form.aiProvider.endpoint}
                onChange={(v) => updateAiProvider('endpoint', v)}
                placeholder={t('placeholder.endpoint')}
              />
            </FieldGroup>
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
          {saved && <span className="text-emerald-400 text-sm">{t('savedSuccess')}</span>}
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
