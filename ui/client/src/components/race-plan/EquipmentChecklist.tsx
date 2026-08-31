import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Plus, Trash2, Shield } from 'lucide-react';
import { toRacePlanUpdateRequest } from '../../types/race-plan';
import type { RacePlanDetail, RacePlanEquipmentItem, EquipmentCategory } from '../../types/race-plan';
import { useUpdateRacePlan } from '../../hooks/useRacePlans';

const CATEGORIES: EquipmentCategory[] = ['clothing', 'footwear', 'navigation', 'nutrition', 'safety', 'lighting', 'other'];

interface Props {
  plan: RacePlanDetail;
  readOnly?: boolean;
}

export default function EquipmentChecklist({ plan, readOnly }: Props) {
  const { t } = useTranslation('race-plans');
  const updatePlan = useUpdateRacePlan();
  const [showForm, setShowForm] = useState(false);
  const [form, setForm] = useState<RacePlanEquipmentItem>({
    name: '',
    category: 'other',
    isMandatory: false,
    notes: undefined,
  });

  const items: RacePlanEquipmentItem[] = plan.equipment ?? [];

  async function saveItems(updated: RacePlanEquipmentItem[]) {
    const req = toRacePlanUpdateRequest(plan, { equipment: updated });
    await updatePlan.mutateAsync({ id: plan.id, data: req });
  }

  async function handleAdd(e: React.FormEvent) {
    e.preventDefault();
    if (!form.name.trim()) return;
    await saveItems([...items, form]);
    setForm({ name: '', category: 'other', isMandatory: false });
    setShowForm(false);
  }

  async function handleRemove(index: number) {
    await saveItems(items.filter((_, i) => i !== index));
  }

  // Group by category
  const byCategory: Record<EquipmentCategory, RacePlanEquipmentItem[]> = {
    clothing: [],
    footwear: [],
    navigation: [],
    nutrition: [],
    safety: [],
    lighting: [],
    other: [],
  };
  items.forEach((item) => {
    byCategory[item.category] = [...(byCategory[item.category] ?? []), item];
  });

  const populatedCategories = CATEGORIES.filter((c) => byCategory[c].length > 0);

  return (
    <div className="space-y-4">
      {items.length === 0 && !showForm && (
        <div className="text-center py-8 text-content-muted/60 text-sm">
          <Shield className="w-8 h-8 mx-auto mb-2 opacity-40" />
          <p>No equipment items yet</p>
        </div>
      )}

      {populatedCategories.map((cat) => (
        <div key={cat}>
          <h4 className="text-xs font-medium text-content-muted uppercase tracking-wide mb-2">
            {t(`equipment.categories.${cat}`)}
          </h4>
          <div className="space-y-1">
            {byCategory[cat].map((item, i) => (
              <div
                key={i}
                className="flex items-center justify-between bg-surface-alt/20 rounded-lg px-3 py-2 group"
              >
                <div className="flex items-center gap-2 min-w-0">
                  {item.isMandatory && (
                    <span className="text-xs px-1.5 py-0.5 rounded bg-red-500/15 text-red-400 shrink-0">
                      {t('equipment.mandatory')}
                    </span>
                  )}
                  <span className="text-sm text-content truncate">{item.name}</span>
                  {item.notes && (
                    <span className="text-xs text-content-muted/60 truncate">{item.notes}</span>
                  )}
                </div>
                {!readOnly && (
                  <button
                    onClick={() => handleRemove(items.indexOf(item))}
                    className="opacity-0 group-hover:opacity-100 p-1 rounded text-content-muted hover:text-red-400 transition-all"
                  >
                    <Trash2 size={13} />
                  </button>
                )}
              </div>
            ))}
          </div>
        </div>
      ))}

      {/* Add form */}
      {!readOnly && (
        showForm ? (
          <form onSubmit={handleAdd} className="bg-surface-alt/30 rounded-xl p-4 space-y-3 border border-border/50">
            <div className="grid grid-cols-2 gap-2">
              <input
                type="text"
                value={form.name}
                onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
                placeholder={t('equipment.name')}
                required
                className="col-span-2 bg-surface border border-border rounded-lg px-3 py-2 text-sm text-content focus:outline-none focus:ring-2 focus:ring-cyan-500"
              />
              <select
                value={form.category}
                onChange={(e) => setForm((f) => ({ ...f, category: e.target.value as EquipmentCategory }))}
                className="bg-surface border border-border rounded-lg px-3 py-2 text-sm text-content focus:outline-none focus:ring-2 focus:ring-cyan-500"
              >
                {CATEGORIES.map((c) => (
                  <option key={c} value={c}>{t(`equipment.categories.${c}`)}</option>
                ))}
              </select>
              <label className="flex items-center gap-2 cursor-pointer">
                <input
                  type="checkbox"
                  checked={form.isMandatory}
                  onChange={(e) => setForm((f) => ({ ...f, isMandatory: e.target.checked }))}
                  className="accent-cyan-500"
                />
                <span className="text-sm text-content">{t('equipment.mandatory')}</span>
              </label>
              <input
                type="text"
                value={form.notes ?? ''}
                onChange={(e) => setForm((f) => ({ ...f, notes: e.target.value || undefined }))}
                placeholder={t('equipment.notes')}
                className="col-span-2 bg-surface border border-border rounded-lg px-3 py-2 text-sm text-content focus:outline-none focus:ring-2 focus:ring-cyan-500"
              />
            </div>
            <div className="flex gap-2">
              <button
                type="button"
                onClick={() => setShowForm(false)}
                className="flex-1 py-2 rounded-lg border border-border text-content-muted hover:text-content text-sm transition-colors"
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={updatePlan.isPending}
                className="flex-1 py-2 rounded-lg bg-cyan-600 hover:bg-cyan-500 disabled:opacity-50 text-white text-sm font-medium transition-colors"
              >
                {updatePlan.isPending ? '…' : t('equipment.addItem')}
              </button>
            </div>
          </form>
        ) : (
          <button
            onClick={() => setShowForm(true)}
            className="w-full py-2 border border-dashed border-border text-content-muted hover:text-content hover:border-content-muted/40 rounded-lg text-sm transition-colors flex items-center justify-center gap-2"
          >
            <Plus size={14} />
            {t('equipment.addItem')}
          </button>
        )
      )}
    </div>
  );
}
