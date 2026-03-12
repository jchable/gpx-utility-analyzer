import { useState, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { useUserProfile, useUpdateUserProfile, useChangePassword } from '../hooks/useActivities';
import type { UpdateProfile } from '../types/activity';

function SectionCard({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="bg-surface-card rounded-xl border border-border p-6">
      <h2 className="text-lg font-semibold text-content mb-4">{title}</h2>
      {children}
    </div>
  );
}

function FieldGroup({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="mb-4 last:mb-0">
      <label className="block text-sm font-medium text-content-muted mb-1.5">{label}</label>
      {children}
    </div>
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
  type?: 'text' | 'password' | 'number' | 'date';
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

function ReadOnlyField({ label, value }: { label: string; value?: string | number }) {
  if (value === undefined || value === null) return null;
  return (
    <div>
      <span className="text-xs text-content-muted">{label}</span>
      <p className="text-sm text-content font-medium">{value}</p>
    </div>
  );
}

export default function ProfilePage() {
  const { t } = useTranslation('profile');
  const { data: profile, isLoading } = useUserProfile();
  const updateMutation = useUpdateUserProfile();
  const passwordMutation = useChangePassword();

  const [savedProfile, setSavedProfile] = useState(false);
  const [savedPassword, setSavedPassword] = useState(false);
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');

  const [edits, setEdits] = useState<UpdateProfile | null>(null);

  const form = useMemo((): UpdateProfile | null => {
    if (!profile) return null;
    const base: UpdateProfile = {
      displayName: profile.displayName ?? '',
      bio: profile.bio ?? '',
      city: profile.city ?? '',
      preferredUnits: profile.preferredUnits ?? 'metric',
      language: profile.language ?? '',
      weightKg: profile.weightKg,
      heightCm: profile.heightCm,
      sex: profile.sex ?? '',
      dateOfBirth: profile.dateOfBirth ? profile.dateOfBirth.substring(0, 10) : '',
      maxHeartRate: profile.maxHeartRate,
      restingHeartRate: profile.restingHeartRate,
      ftp: profile.ftp,
      vo2Max: profile.vo2Max,
    };
    return edits ? { ...base, ...edits } : base;
  }, [profile, edits]);

  const set = (field: keyof UpdateProfile, value: string | number | undefined) => {
    setEdits((prev) => ({ ...(prev ?? {}), [field]: value }));
  };

  if (isLoading || !form || !profile) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-8 w-8 border-2 border-blue-500 border-t-transparent" />
      </div>
    );
  }

  const handleSaveProfile = async () => {
    setSavedProfile(false);
    const payload: UpdateProfile = { ...form };
    // Convert empty strings to undefined for optional fields
    if (!payload.bio) payload.bio = undefined;
    if (!payload.city) payload.city = undefined;
    if (!payload.language) payload.language = undefined;
    if (!payload.sex) payload.sex = undefined;
    if (!payload.dateOfBirth) payload.dateOfBirth = undefined;
    await updateMutation.mutateAsync(payload);
    setSavedProfile(true);
    setTimeout(() => setSavedProfile(false), 3000);
  };

  const handleChangePassword = async () => {
    setSavedPassword(false);
    await passwordMutation.mutateAsync({ currentPassword, newPassword });
    setSavedPassword(true);
    setCurrentPassword('');
    setNewPassword('');
    setTimeout(() => setSavedPassword(false), 3000);
  };

  const unitsOptions = [
    { value: 'metric', label: t('units.metric') },
    { value: 'imperial', label: t('units.imperial') },
  ];

  const sexOptions = [
    { value: '', label: '—' },
    { value: 'male', label: t('sex.male') },
    { value: 'female', label: t('sex.female') },
    { value: 'other', label: t('sex.other') },
  ];

  return (
    <div>
      <h1 className="text-2xl font-bold text-content mb-6">{t('title')}</h1>

      <div className="mb-2 text-sm text-content-muted">
        {profile.email}
      </div>

      <div className="space-y-6">
        {/* Personal Information */}
        <SectionCard title={t('section.personal')}>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <FieldGroup label={t('displayName')}>
              <TextInput
                value={form.displayName ?? ''}
                onChange={(v) => set('displayName', v)}
                placeholder={t('placeholder.displayName')}
              />
            </FieldGroup>
            <FieldGroup label={t('city')}>
              <TextInput
                value={form.city ?? ''}
                onChange={(v) => set('city', v)}
                placeholder={t('placeholder.city')}
              />
            </FieldGroup>
            <div className="col-span-full">
              <FieldGroup label={t('bio')}>
                <textarea
                  value={form.bio ?? ''}
                  onChange={(e) => set('bio', e.target.value)}
                  placeholder={t('placeholder.bio')}
                  rows={3}
                  className="w-full bg-surface-input border border-border rounded-lg px-3 py-2 text-content text-sm focus:outline-none focus:border-blue-500/50 placeholder-content-muted/60 resize-none"
                />
              </FieldGroup>
            </div>
            <FieldGroup label={t('preferredUnits')}>
              <SelectInput
                value={form.preferredUnits ?? 'metric'}
                onChange={(v) => set('preferredUnits', v)}
                options={unitsOptions}
              />
            </FieldGroup>
          </div>
        </SectionCard>

        {/* Physical Measurements */}
        <SectionCard title={t('section.biometrics')}>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            <FieldGroup label={t('weightKg')}>
              <TextInput
                value={form.weightKg?.toString() ?? ''}
                onChange={(v) => set('weightKg', v === '' ? undefined : Number(v))}
                placeholder={t('placeholder.weightKg')}
                type="number"
              />
            </FieldGroup>
            <FieldGroup label={t('heightCm')}>
              <TextInput
                value={form.heightCm?.toString() ?? ''}
                onChange={(v) => set('heightCm', v === '' ? undefined : Number(v))}
                placeholder={t('placeholder.heightCm')}
                type="number"
              />
            </FieldGroup>
            <FieldGroup label={t('sex')}>
              <SelectInput
                value={form.sex ?? ''}
                onChange={(v) => set('sex', v)}
                options={sexOptions}
              />
            </FieldGroup>
            <FieldGroup label={t('dateOfBirth')}>
              <TextInput
                value={form.dateOfBirth ?? ''}
                onChange={(v) => set('dateOfBirth', v)}
                type="date"
              />
            </FieldGroup>
          </div>

          {/* Computed values */}
          {(profile.age !== undefined || profile.bmi !== undefined || profile.estimatedMaxHR !== undefined) && (
            <div className="mt-4 pt-4 border-t border-border">
              <p className="text-xs text-content-muted mb-3">{t('computed.hint')}</p>
              <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
                <ReadOnlyField label={t('age')} value={profile.age} />
                <ReadOnlyField label={t('bmi')} value={profile.bmi !== undefined ? Number(profile.bmi.toFixed(1)) : undefined} />
                <ReadOnlyField label={t('estimatedMaxHR')} value={profile.estimatedMaxHR} />
              </div>
            </div>
          )}
        </SectionCard>

        {/* Performance */}
        <SectionCard title={t('section.performance')}>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            <FieldGroup label={t('maxHeartRate')}>
              <TextInput
                value={form.maxHeartRate?.toString() ?? ''}
                onChange={(v) => set('maxHeartRate', v === '' ? undefined : Number(v))}
                placeholder={t('placeholder.maxHeartRate')}
                type="number"
              />
            </FieldGroup>
            <FieldGroup label={t('restingHeartRate')}>
              <TextInput
                value={form.restingHeartRate?.toString() ?? ''}
                onChange={(v) => set('restingHeartRate', v === '' ? undefined : Number(v))}
                placeholder={t('placeholder.restingHeartRate')}
                type="number"
              />
            </FieldGroup>
            <FieldGroup label={t('ftp')}>
              <TextInput
                value={form.ftp?.toString() ?? ''}
                onChange={(v) => set('ftp', v === '' ? undefined : Number(v))}
                placeholder={t('placeholder.ftp')}
                type="number"
              />
            </FieldGroup>
            <FieldGroup label={t('vo2max')}>
              <TextInput
                value={form.vo2Max?.toString() ?? ''}
                onChange={(v) => set('vo2Max', v === '' ? undefined : Number(v))}
                placeholder={t('placeholder.vo2max')}
                type="number"
              />
            </FieldGroup>
          </div>
        </SectionCard>

        {/* Save profile button */}
        <div className="flex items-center gap-3">
          <button
            onClick={handleSaveProfile}
            disabled={updateMutation.isPending}
            className="px-6 py-2.5 bg-blue-600 hover:bg-blue-500 disabled:bg-blue-600/50 text-white font-medium rounded-lg transition-colors"
          >
            {updateMutation.isPending ? t('saving') : t('saveProfile')}
          </button>
          {savedProfile && <span className="text-emerald-400 text-sm">{t('savedSuccess')}</span>}
          {updateMutation.isError && (
            <span className="text-red-400 text-sm">{t('saveFailed', { message: updateMutation.error.message })}</span>
          )}
        </div>

        {/* Change Password */}
        <SectionCard title={t('section.password')}>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <FieldGroup label={t('currentPassword')}>
              <TextInput
                value={currentPassword}
                onChange={setCurrentPassword}
                placeholder={t('placeholder.currentPassword')}
                type="password"
              />
            </FieldGroup>
            <FieldGroup label={t('newPassword')}>
              <TextInput
                value={newPassword}
                onChange={setNewPassword}
                placeholder={t('placeholder.newPassword')}
                type="password"
              />
            </FieldGroup>
          </div>
          <div className="flex items-center gap-3 mt-4">
            <button
              onClick={handleChangePassword}
              disabled={passwordMutation.isPending || !currentPassword || !newPassword}
              className="px-5 py-2 bg-surface-alt hover:bg-surface-alt/70 disabled:opacity-40 text-content text-sm font-medium rounded-lg border border-border transition-colors"
            >
              {passwordMutation.isPending ? t('changingPassword') : t('changePassword')}
            </button>
            {savedPassword && <span className="text-emerald-400 text-sm">{t('passwordChanged')}</span>}
            {passwordMutation.isError && (
              <span className="text-red-400 text-sm">{t('passwordFailed', { message: passwordMutation.error.message })}</span>
            )}
          </div>
        </SectionCard>
      </div>
    </div>
  );
}
