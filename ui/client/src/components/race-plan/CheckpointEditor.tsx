import { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { X, Plus, Minus } from 'lucide-react';
import type { RacePlanDetail, RacePlanCheckpointCreateRequest, CheckpointType, DropBagItem } from '../../types/race-plan';
import { useAddCheckpoint, useUpdateCheckpoint } from '../../hooks/useRacePlans';
import { useRacePlanStore } from '../../stores/racePlanStore';
import { formatArrivalTime } from '../../utils/dayNight';

const CHECKPOINT_TYPES: CheckpointType[] = ['start', 'checkpoint', 'aid_station', 'crew_only', 'finish'];

interface Props {
  plan: RacePlanDetail;
}

export default function CheckpointEditor({ plan }: Props) {
  const { t } = useTranslation('race-plans');
  const { t: tc } = useTranslation();
  const { editingCheckpointId, newCheckpointDistanceKm, closeCheckpointEditor } = useRacePlanStore();
  const addCheckpoint = useAddCheckpoint();
  const updateCheckpoint = useUpdateCheckpoint();

  const existing = editingCheckpointId
    ? plan.checkpoints.find((cp) => cp.id === editingCheckpointId)
    : null;

  const [form, setForm] = useState<RacePlanCheckpointCreateRequest>({
    name: '',
    type: 'aid_station',
    distanceKm: newCheckpointDistanceKm ?? 0,
    cutoffTimeSeconds: null,
    plannedPauseSeconds: null,
    isCrewAccessible: false,
    crewNotes: null,
    hasDropBag: false,
    dropBagContents: null,
    notes: null,
  });

  // Populate form when editing existing.
  // Intentional prop→form sync: the editable form must reset when the selected
  // checkpoint (or the new-checkpoint distance) changes.
  useEffect(() => {
    if (existing) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setForm({
        name: existing.name,
        type: existing.type,
        distanceKm: existing.distanceKm,
        cutoffTimeSeconds: existing.cutoffTimeSeconds,
        plannedPauseSeconds: existing.plannedPauseSeconds,
        isCrewAccessible: existing.isCrewAccessible,
        crewNotes: existing.crewNotes,
        hasDropBag: existing.hasDropBag,
        dropBagContents: existing.dropBagContents,
        notes: existing.notes,
      });
    } else {
      setForm((f) => ({ ...f, distanceKm: newCheckpointDistanceKm ?? f.distanceKm }));
    }
  }, [existing, editingCheckpointId, newCheckpointDistanceKm]);

  // Helpers for cutoff / pause time inputs (HH:mm format ↔ seconds)
  const startTime = plan.startTime ?? '00:00';

  function secondsToHHMM(seconds: number | null | undefined): string {
    if (seconds == null) return '';
    return formatArrivalTime(startTime, seconds);
  }

  function hhmmToSeconds(hhmmValue: string): number | null {
    if (!hhmmValue) return null;
    const [hh, mm] = hhmmValue.split(':').map(Number);
    const [sh, sm] = startTime.split(':').map(Number);
    const startMinutes = sh * 60 + sm;
    const targetMinutes = hh * 60 + mm;
    let diffMinutes = targetMinutes - startMinutes;
    if (diffMinutes < 0) diffMinutes += 24 * 60; // next day
    return diffMinutes * 60;
  }

  function minutesToSeconds(min: string): number | null {
    const v = parseInt(min, 10);
    if (isNaN(v) || v <= 0) return null;
    return v * 60;
  }

  function secondsToMinutes(sec: number | null | undefined): string {
    if (sec == null || sec <= 0) return '';
    return String(Math.round(sec / 60));
  }

  // Drop bag items
  const dropBagItems: DropBagItem[] = form.dropBagContents ?? [];

  function setDropBagItem(index: number, field: 'item' | 'qty', value: string) {
    const updated = [...dropBagItems];
    if (field === 'item') updated[index] = { ...updated[index], item: value };
    else updated[index] = { ...updated[index], qty: parseInt(value, 10) || 1 };
    setForm((f) => ({ ...f, dropBagContents: updated }));
  }

  function addDropBagItem() {
    setForm((f) => ({ ...f, dropBagContents: [...dropBagItems, { item: '', qty: 1 }] }));
  }

  function removeDropBagItem(index: number) {
    setForm((f) => ({ ...f, dropBagContents: dropBagItems.filter((_, i) => i !== index) }));
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const payload = {
      ...form,
      dropBagContents: form.hasDropBag && (form.dropBagContents?.length ?? 0) > 0
        ? form.dropBagContents
        : null,
    };
    if (existing) {
      await updateCheckpoint.mutateAsync({ planId: plan.id, checkpointId: existing.id, data: payload });
    } else {
      await addCheckpoint.mutateAsync({ planId: plan.id, data: payload });
    }
    closeCheckpointEditor();
  }

  const isPending = addCheckpoint.isPending || updateCheckpoint.isPending;

  return (
    <div className="fixed inset-y-0 right-0 z-40 w-full sm:max-w-md bg-surface border-l border-border shadow-2xl flex flex-col">
      {/* Header */}
      <div className="flex items-center justify-between px-5 py-4 border-b border-border shrink-0">
        <h3 className="text-lg font-semibold text-content">
          {existing ? t('checkpoint.edit') : t('checkpoint.add')}
        </h3>
        <button
          onClick={closeCheckpointEditor}
          className="p-1.5 rounded-lg text-content-muted hover:text-content hover:bg-surface-alt/50 transition-colors"
        >
          <X size={18} />
        </button>
      </div>

      {/* Scrollable form body */}
      <form onSubmit={handleSubmit} className="flex-1 overflow-y-auto px-5 py-4 space-y-4">
        {/* Name */}
        <div>
          <label className="block text-xs font-medium text-content-muted mb-1">{t('checkpoint.name')}</label>
          <input
            type="text"
            value={form.name}
            onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
            required
            className="w-full bg-surface-alt border border-border rounded-lg px-3 py-2 text-sm text-content focus:outline-none focus:ring-2 focus:ring-cyan-500"
          />
        </div>

        {/* Type */}
        <div>
          <label className="block text-xs font-medium text-content-muted mb-1">{t('checkpoint.type')}</label>
          <select
            value={form.type}
            onChange={(e) => setForm((f) => ({ ...f, type: e.target.value as CheckpointType }))}
            className="w-full bg-surface-alt border border-border rounded-lg px-3 py-2 text-sm text-content focus:outline-none focus:ring-2 focus:ring-cyan-500"
          >
            {CHECKPOINT_TYPES.map((type) => (
              <option key={type} value={type}>{t(`checkpoint.types.${type}`)}</option>
            ))}
          </select>
        </div>

        {/* Distance */}
        <div>
          <label className="block text-xs font-medium text-content-muted mb-1">{t('checkpoint.distance')}</label>
          <input
            type="number"
            min={0}
            max={plan.distanceKm}
            step={0.1}
            value={form.distanceKm}
            onChange={(e) => setForm((f) => ({ ...f, distanceKm: parseFloat(e.target.value) || 0 }))}
            required
            className="w-full bg-surface-alt border border-border rounded-lg px-3 py-2 text-sm text-content focus:outline-none focus:ring-2 focus:ring-cyan-500"
          />
          <p className="text-xs text-content-muted mt-1">
            {t('stats.distance')}: 0 – {plan.distanceKm.toFixed(1)} km
          </p>
        </div>

        {/* Cutoff time */}
        <div>
          <label className="block text-xs font-medium text-content-muted mb-1">
            {t('checkpoint.cutoff')} (HH:mm)
          </label>
          <input
            type="time"
            value={secondsToHHMM(form.cutoffTimeSeconds)}
            onChange={(e) => setForm((f) => ({ ...f, cutoffTimeSeconds: hhmmToSeconds(e.target.value) }))}
            className="w-full bg-surface-alt border border-border rounded-lg px-3 py-2 text-sm text-content focus:outline-none focus:ring-2 focus:ring-cyan-500"
          />
        </div>

        {/* Planned pause */}
        <div>
          <label className="block text-xs font-medium text-content-muted mb-1">
            {t('checkpoint.pause')} (min)
          </label>
          <input
            type="number"
            min={0}
            step={1}
            value={secondsToMinutes(form.plannedPauseSeconds)}
            onChange={(e) => setForm((f) => ({ ...f, plannedPauseSeconds: minutesToSeconds(e.target.value) }))}
            className="w-full bg-surface-alt border border-border rounded-lg px-3 py-2 text-sm text-content focus:outline-none focus:ring-2 focus:ring-cyan-500"
          />
        </div>

        {/* Crew accessible */}
        <div className="flex items-center gap-3">
          <input
            type="checkbox"
            id="crewAccess"
            checked={form.isCrewAccessible}
            onChange={(e) => setForm((f) => ({ ...f, isCrewAccessible: e.target.checked }))}
            className="w-4 h-4 rounded border-border accent-cyan-500"
          />
          <label htmlFor="crewAccess" className="text-sm text-content cursor-pointer">
            {t('checkpoint.crewAccess')}
          </label>
        </div>

        {form.isCrewAccessible && (
          <div>
            <label className="block text-xs font-medium text-content-muted mb-1">{t('checkpoint.crewNotes')}</label>
            <textarea
              value={form.crewNotes ?? ''}
              onChange={(e) => setForm((f) => ({ ...f, crewNotes: e.target.value || null }))}
              rows={2}
              className="w-full bg-surface-alt border border-border rounded-lg px-3 py-2 text-sm text-content focus:outline-none focus:ring-2 focus:ring-cyan-500 resize-none"
            />
          </div>
        )}

        {/* Drop bag */}
        <div className="flex items-center gap-3">
          <input
            type="checkbox"
            id="dropBag"
            checked={form.hasDropBag}
            onChange={(e) => setForm((f) => ({ ...f, hasDropBag: e.target.checked }))}
            className="w-4 h-4 rounded border-border accent-cyan-500"
          />
          <label htmlFor="dropBag" className="text-sm text-content cursor-pointer">
            {t('checkpoint.dropBag')}
          </label>
        </div>

        {form.hasDropBag && (
          <div className="space-y-2">
            <label className="block text-xs font-medium text-content-muted">{t('checkpoint.dropBagContents')}</label>
            {dropBagItems.map((item, i) => (
              <div key={i} className="flex gap-2 items-center">
                <input
                  type="text"
                  value={item.item}
                  onChange={(e) => setDropBagItem(i, 'item', e.target.value)}
                  placeholder="Item…"
                  className="flex-1 bg-surface-alt border border-border rounded-lg px-3 py-1.5 text-sm text-content focus:outline-none focus:ring-2 focus:ring-cyan-500"
                />
                <input
                  type="number"
                  min={1}
                  value={item.qty}
                  onChange={(e) => setDropBagItem(i, 'qty', e.target.value)}
                  className="w-16 bg-surface-alt border border-border rounded-lg px-2 py-1.5 text-sm text-content focus:outline-none focus:ring-2 focus:ring-cyan-500 text-center"
                />
                <button
                  type="button"
                  onClick={() => removeDropBagItem(i)}
                  className="p-1.5 rounded-lg text-content-muted hover:text-red-400 transition-colors"
                >
                  <Minus size={14} />
                </button>
              </div>
            ))}
            <button
              type="button"
              onClick={addDropBagItem}
              className="flex items-center gap-1.5 text-xs text-content-muted hover:text-content transition-colors"
            >
              <Plus size={13} />
              Add item
            </button>
          </div>
        )}

        {/* Notes */}
        <div>
          <label className="block text-xs font-medium text-content-muted mb-1">{t('checkpoint.notes')}</label>
          <textarea
            value={form.notes ?? ''}
            onChange={(e) => setForm((f) => ({ ...f, notes: e.target.value || null }))}
            rows={3}
            className="w-full bg-surface-alt border border-border rounded-lg px-3 py-2 text-sm text-content focus:outline-none focus:ring-2 focus:ring-cyan-500 resize-none"
          />
        </div>
      </form>

      {/* Footer */}
      <div className="px-5 py-4 border-t border-border shrink-0 flex gap-3">
        <button
          type="button"
          onClick={closeCheckpointEditor}
          className="flex-1 py-2.5 rounded-lg border border-border text-content-muted hover:text-content hover:bg-surface-alt/50 text-sm transition-colors"
        >
          {tc('button.cancel')}
        </button>
        <button
          onClick={handleSubmit}
          disabled={isPending || !form.name}
          className="flex-1 py-2.5 rounded-lg bg-cyan-600 hover:bg-cyan-500 disabled:opacity-50 text-white text-sm font-medium transition-colors"
        >
          {isPending ? '…' : tc('button.save')}
        </button>
      </div>
    </div>
  );
}
