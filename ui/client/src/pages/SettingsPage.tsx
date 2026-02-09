import { useState, useEffect } from 'react';
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

function ConfiguredBadge({ configured }: { configured: boolean }) {
  if (!configured) return null;
  return (
    <span className="ml-2 inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-emerald-500/20 text-emerald-400">
      Configured
    </span>
  );
}

const PRESET_OPTIONS = [
  { value: 'trail', label: 'Trail' },
  { value: 'hiking', label: 'Hiking' },
  { value: 'cycling', label: 'Cycling' },
];

const SMOOTHING_OPTIONS = [
  { value: 'none', label: 'None' },
  { value: 'light', label: 'Light' },
  { value: 'medium', label: 'Medium' },
  { value: 'heavy', label: 'Heavy' },
];

const ELEVATION_ALGORITHM_OPTIONS = [
  { value: 'threshold', label: 'Threshold' },
  { value: 'douglas-peucker', label: 'Douglas-Peucker' },
  { value: 'segments', label: 'Segments' },
];

export default function SettingsPage() {
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

  return (
    <div>
      <h1 className="text-2xl font-bold text-white mb-6">Settings</h1>

      <div className="space-y-6 max-w-2xl">
        {/* Integration Credentials */}
        <SectionCard title="Integration Credentials">
          <div className="space-y-5">
            <div>
              <h3 className="text-sm font-semibold text-white mb-3 flex items-center">
                <span
                  className="inline-flex items-center justify-center w-5 h-5 rounded text-xs font-bold mr-2"
                  style={{ backgroundColor: '#FC4C02', color: 'white' }}
                >
                  S
                </span>
                Strava
              </h3>
              <div className="grid grid-cols-2 gap-4">
                <FieldGroup label="Client ID">
                  <TextInput
                    value={form.integrations.strava.clientId}
                    onChange={(v) => updateStrava('clientId', v)}
                    placeholder="Enter Strava Client ID"
                  />
                </FieldGroup>
                <FieldGroup label={<>Client Secret<ConfiguredBadge configured={settings?.integrations.strava.hasClientSecret ?? false} /></>}>
                  <TextInput
                    value={form.integrations.strava.clientSecret}
                    onChange={(v) => updateStrava('clientSecret', v)}
                    placeholder="Enter new secret to update"
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
                Garmin Connect
              </h3>
              <div className="grid grid-cols-2 gap-4">
                <FieldGroup label="Consumer Key">
                  <TextInput
                    value={form.integrations.garmin.consumerKey}
                    onChange={(v) => updateGarmin('consumerKey', v)}
                    placeholder="Enter Garmin Consumer Key"
                  />
                </FieldGroup>
                <FieldGroup label={<>Consumer Secret<ConfiguredBadge configured={settings?.integrations.garmin.hasConsumerSecret ?? false} /></>}>
                  <TextInput
                    value={form.integrations.garmin.consumerSecret}
                    onChange={(v) => updateGarmin('consumerSecret', v)}
                    placeholder="Enter new secret to update"
                    type="password"
                  />
                </FieldGroup>
              </div>
            </div>
          </div>
        </SectionCard>

        {/* Analysis Preferences */}
        <SectionCard title="Analysis Preferences">
          <div className="grid grid-cols-2 gap-4">
            <FieldGroup label="Default Preset">
              <SelectInput
                value={form.analysis.preset}
                onChange={(v) => updateAnalysis('preset', v)}
                options={PRESET_OPTIONS}
              />
            </FieldGroup>
            <FieldGroup label="Smoothing">
              <SelectInput
                value={form.analysis.smoothing}
                onChange={(v) => updateAnalysis('smoothing', v)}
                options={SMOOTHING_OPTIONS}
              />
            </FieldGroup>
            <FieldGroup label="Track Smoothing">
              <SelectInput
                value={form.analysis.trackSmoothing}
                onChange={(v) => updateAnalysis('trackSmoothing', v)}
                options={SMOOTHING_OPTIONS}
              />
            </FieldGroup>
            <FieldGroup label="Elevation Algorithm">
              <SelectInput
                value={form.analysis.elevationAlgorithm}
                onChange={(v) => updateAnalysis('elevationAlgorithm', v)}
                options={ELEVATION_ALGORITHM_OPTIONS}
              />
            </FieldGroup>
          </div>
        </SectionCard>

        {/* AI Provider */}
        <SectionCard title="AI Provider">
          <div className="grid grid-cols-2 gap-4">
            <FieldGroup label="Provider">
              <SelectInput
                value={form.aiProvider.name}
                onChange={(v) => updateAiProvider('name', v)}
                options={[{ value: '', label: 'Select provider...' }, ...providerOptions]}
              />
            </FieldGroup>
            <FieldGroup label="Model">
              <TextInput
                value={form.aiProvider.model}
                onChange={(v) => updateAiProvider('model', v)}
                placeholder="e.g. gemini-2.5-flash"
              />
            </FieldGroup>
            <FieldGroup label={<>API Key<ConfiguredBadge configured={settings?.aiProvider.hasApiKey ?? false} /></>}>
              <TextInput
                value={form.aiProvider.apiKey}
                onChange={(v) => updateAiProvider('apiKey', v)}
                placeholder="Enter new key to update"
                type="password"
              />
            </FieldGroup>
            <FieldGroup label="Endpoint (optional)">
              <TextInput
                value={form.aiProvider.endpoint}
                onChange={(v) => updateAiProvider('endpoint', v)}
                placeholder="Custom endpoint URL"
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
            {updateMutation.isPending ? 'Saving...' : 'Save Settings'}
          </button>
          {saved && (
            <span className="text-emerald-400 text-sm">Settings saved successfully.</span>
          )}
          {updateMutation.isError && (
            <span className="text-red-400 text-sm">
              Failed to save: {updateMutation.error.message}
            </span>
          )}
        </div>
      </div>
    </div>
  );
}
