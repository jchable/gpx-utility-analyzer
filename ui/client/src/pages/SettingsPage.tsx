import { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { useSettings, useUpdateSettings } from '../hooks/useActivities';
import type { AppSettings } from '../types/activity';

function SectionCard({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="bg-[#16213e] rounded-xl border border-slate-700/50 p-6">
      <h2 className="text-lg font-semibold text-white mb-4">{title}</h2>
      {children}
    </div>
  );
}

function FieldGroup({ label, children }: { label: React.ReactNode; children: React.ReactNode }) {
  return (
    <div className="mb-4 last:mb-0">
      <label className="block text-sm font-medium text-[#a0a0b0] mb-1.5">{label}</label>
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
      className="w-full bg-[#0d1b2a] border border-slate-700/50 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-blue-500/50"
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
  type?: 'text' | 'password';
}) {
  return (
    <input
      type={type}
      value={value}
      onChange={(e) => onChange(e.target.value)}
      placeholder={placeholder}
      className="w-full bg-[#0d1b2a] border border-slate-700/50 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-blue-500/50 placeholder-slate-600"
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

export default function SettingsPage() {
  const { t } = useTranslation('settings');
  const { data: settings, isLoading } = useSettings();
  const updateMutation = useUpdateSettings();

  const [form, setForm] = useState<AppSettings | null>(null);
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    if (settings && !form) {
      setForm({
        ...settings,
        aiProvider: { ...settings.aiProvider, apiKey: '' },
        integrations: {
          strava: { ...settings.integrations.strava, clientSecret: '' },
          garmin: { ...settings.integrations.garmin, consumerSecret: '' },
        },
      });
    }
  }, [settings, form]);

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

  const updateAnalysis = (field: string, value: string) => {
    setForm((f) => f ? { ...f, analysis: { ...f.analysis, [field]: value } } : f);
  };

  const updateAiProvider = (field: string, value: string) => {
    setForm((f) => f ? { ...f, aiProvider: { ...f.aiProvider, [field]: value } } : f);
  };

  const updateStrava = (field: string, value: string) => {
    setForm((f) =>
      f
        ? {
            ...f,
            integrations: {
              ...f.integrations,
              strava: { ...f.integrations.strava, [field]: value },
            },
          }
        : f,
    );
  };

  const updateGarmin = (field: string, value: string) => {
    setForm((f) =>
      f
        ? {
            ...f,
            integrations: {
              ...f.integrations,
              garmin: { ...f.integrations.garmin, [field]: value },
            },
          }
        : f,
    );
  };

  const providerOptions = (settings?.aiProvider.availableProviders ?? []).map((p) => ({
    value: p,
    label: p.charAt(0).toUpperCase() + p.slice(1),
  }));

  const presetOptions = [
    { value: 'trail', label: t('preset.trail') },
    { value: 'hiking', label: t('preset.hiking') },
    { value: 'cycling', label: t('preset.cycling') },
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
      <h1 className="text-2xl font-bold text-white mb-6">{t('title')}</h1>

      <div className="space-y-6 max-w-2xl">
        {/* Integration Credentials */}
        <SectionCard title={t('integrationCredentials')}>
          <div className="space-y-5">
            <div>
              <h3 className="text-sm font-semibold text-white mb-3 flex items-center">
                <span
                  className="inline-flex items-center justify-center w-5 h-5 rounded text-xs font-bold mr-2"
                  style={{ backgroundColor: '#FC4C02', color: 'white' }}
                >
                  S
                </span>
                {t('strava')}
              </h3>
              <div className="grid grid-cols-2 gap-4">
                <FieldGroup label={t('clientId')}>
                  <TextInput
                    value={form.integrations.strava.clientId}
                    onChange={(v) => updateStrava('clientId', v)}
                    placeholder={t('placeholder.stravaClientId')}
                  />
                </FieldGroup>
                <FieldGroup label={<>{t('clientSecret')}{(settings?.integrations.strava.hasClientSecret ?? false) && <ConfiguredBadge label={t('configured')} />}</>}>
                  <TextInput
                    value={form.integrations.strava.clientSecret}
                    onChange={(v) => updateStrava('clientSecret', v)}
                    placeholder={t('placeholder.stravaClientSecret')}
                    type="password"
                  />
                </FieldGroup>
              </div>
            </div>

            <div className="border-t border-slate-700/50 pt-5">
              <h3 className="text-sm font-semibold text-white mb-3 flex items-center">
                <span
                  className="inline-flex items-center justify-center w-5 h-5 rounded text-xs font-bold mr-2"
                  style={{ backgroundColor: '#007CC3', color: 'white' }}
                >
                  G
                </span>
                {t('garminConnect')}
              </h3>
              <div className="grid grid-cols-2 gap-4">
                <FieldGroup label={t('consumerKey')}>
                  <TextInput
                    value={form.integrations.garmin.consumerKey}
                    onChange={(v) => updateGarmin('consumerKey', v)}
                    placeholder={t('placeholder.garminConsumerKey')}
                  />
                </FieldGroup>
                <FieldGroup label={<>{t('consumerSecret')}{(settings?.integrations.garmin.hasConsumerSecret ?? false) && <ConfiguredBadge label={t('configured')} />}</>}>
                  <TextInput
                    value={form.integrations.garmin.consumerSecret}
                    onChange={(v) => updateGarmin('consumerSecret', v)}
                    placeholder={t('placeholder.garminConsumerSecret')}
                    type="password"
                  />
                </FieldGroup>
              </div>
            </div>
          </div>
        </SectionCard>

        {/* Analysis Preferences */}
        <SectionCard title={t('analysisPreferences')}>
          <div className="grid grid-cols-2 gap-4">
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
          </div>
        </SectionCard>

        {/* AI Provider */}
        <SectionCard title={t('aiProvider')}>
          <div className="grid grid-cols-2 gap-4">
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
            <FieldGroup label={<>{t('apiKey')}{(settings?.aiProvider.hasApiKey ?? false) && <ConfiguredBadge label={t('configured')} />}</>}>
              <TextInput
                value={form.aiProvider.apiKey}
                onChange={(v) => updateAiProvider('apiKey', v)}
                placeholder={t('placeholder.apiKey')}
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
