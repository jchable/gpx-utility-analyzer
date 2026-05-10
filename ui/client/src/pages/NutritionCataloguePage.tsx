import { useState, useRef, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import { Download, Trash2, Plus } from 'lucide-react';
import {
  useNutritionProducts,
  useCreateNutritionProduct,
  useUpdateNutritionProduct,
  useDeleteNutritionProduct,
  useImportDefaultProducts,
} from '../hooks/useRacePlans';
import type { NutritionProduct, NutritionProductCreateRequest, NutritionProductType } from '../types/race-plan';

const TYPES: NutritionProductType[] = ['gel', 'bar', 'drink', 'real_food', 'electrolyte', 'supplement'];

const TYPE_COLORS: Record<NutritionProductType, string> = {
  gel: 'text-cyan-400 bg-cyan-400/10 border-cyan-400/20',
  bar: 'text-amber-400 bg-amber-400/10 border-amber-400/20',
  drink: 'text-blue-400 bg-blue-400/10 border-blue-400/20',
  real_food: 'text-green-400 bg-green-400/10 border-green-400/20',
  electrolyte: 'text-purple-400 bg-purple-400/10 border-purple-400/20',
  supplement: 'text-orange-400 bg-orange-400/10 border-orange-400/20',
};

const emptyNew = (): NutritionProductCreateRequest => ({
  name: '',
  brand: null,
  type: 'gel',
  caloriesKcal: 0,
  carbsG: 0,
  proteinsG: null,
  fatsG: null,
  sodiumMg: null,
  caffeineG: null,
  weightG: null,
  volumeML: null,
  notes: null,
});

function n(v: string): number | null {
  const p = parseFloat(v);
  return isNaN(p) || v === '' ? null : p;
}

function num(v: string): number {
  const p = parseFloat(v);
  return isNaN(p) ? 0 : p;
}

// ─── Cellule éditable ────────────────────────────────────────────────────────

function EditCell({
  value,
  type = 'number',
  onCommit,
  className = '',
  placeholder = '—',
}: {
  value: string | number | null;
  type?: 'text' | 'number';
  onCommit: (v: string) => void;
  className?: string;
  placeholder?: string;
}) {
  const ref = useRef<HTMLInputElement>(null);
  return (
    <input
      ref={ref}
      type={type}
      defaultValue={value ?? ''}
      placeholder={placeholder}
      onClick={(e) => e.stopPropagation()}
      onBlur={(e) => onCommit(e.target.value)}
      onKeyDown={(e) => {
        if (e.key === 'Enter') ref.current?.blur();
        if (e.key === 'Escape') { e.currentTarget.value = String(value ?? ''); ref.current?.blur(); }
      }}
      className={`w-full bg-surface-alt border border-border/60 rounded px-1.5 py-0.5 text-sm text-content focus:outline-none focus:ring-1 focus:ring-cyan-500 ${className}`}
    />
  );
}

// ─── Ligne produit existant ───────────────────────────────────────────────────

function ProductRow({
  product,
  editing,
  onStartEdit,
  onUpdate,
  onDelete,
  showWeight,
  showVolume,
}: {
  product: NutritionProduct;
  editing: boolean;
  onStartEdit: () => void;
  onUpdate: (patch: Partial<NutritionProductCreateRequest>) => void;
  onDelete: () => void;
  showWeight: boolean;
  showVolume: boolean;
}) {
  const { t } = useTranslation('race-plans');
  const colorCls = TYPE_COLORS[product.type as NutritionProductType] ?? 'text-content bg-surface-alt/30 border-transparent';

  return (
    <tr
      onClick={onStartEdit}
      className={`border-b border-border/40 transition-colors cursor-pointer ${editing ? 'bg-surface-alt/60' : 'hover:bg-surface-alt/30'}`}
    >
      {/* Type */}
      <td className="px-3 py-2 w-28">
        {editing ? (
          <select
            value={product.type}
            onClick={(e) => e.stopPropagation()}
            onChange={(e) => onUpdate({ type: e.target.value as NutritionProductType })}
            className="w-full bg-surface-alt border border-border/60 rounded px-1.5 py-0.5 text-xs text-content focus:outline-none focus:ring-1 focus:ring-cyan-500"
          >
            {TYPES.map((tp) => (
              <option key={tp} value={tp}>{t(`nutritionCatalogue.types.${tp}`)}</option>
            ))}
          </select>
        ) : (
          <span className={`text-xs font-medium px-2 py-0.5 rounded-full border ${colorCls}`}>
            {t(`nutritionCatalogue.types.${product.type}`)}
          </span>
        )}
      </td>

      {/* Nom + marque */}
      <td className="px-3 py-2">
        {editing ? (
          <div className="flex gap-1">
            <EditCell type="text" value={product.brand} onCommit={(v) => onUpdate({ brand: v || null })} placeholder={t('nutritionCatalogue.brand')} className="w-24 text-xs text-content-muted" />
            <EditCell type="text" value={product.name} onCommit={(v) => onUpdate({ name: v })} placeholder={t('nutritionCatalogue.name')} className="flex-1" />
          </div>
        ) : (
          <div>
            <span className="text-sm font-medium text-content">{product.name}</span>
            {product.brand && <span className="ml-2 text-xs text-content-muted">{product.brand}</span>}
          </div>
        )}
      </td>

      {/* kcal */}
      <td className="px-3 py-2 text-right text-sm font-bold text-orange-400 w-16">
        {editing
          ? <EditCell value={product.caloriesKcal} onCommit={(v) => onUpdate({ caloriesKcal: num(v) })} className="text-right" />
          : product.caloriesKcal}
      </td>

      {/* Glucides */}
      <td className="px-3 py-2 text-right text-sm text-yellow-400 w-20">
        {editing
          ? <EditCell value={product.carbsG} onCommit={(v) => onUpdate({ carbsG: num(v) })} className="text-right" />
          : `${product.carbsG}g`}
      </td>

      {/* Protéines */}
      <td className="px-3 py-2 text-right text-sm text-content-muted w-20">
        {editing
          ? <EditCell value={product.proteinsG} onCommit={(v) => onUpdate({ proteinsG: n(v) })} className="text-right" />
          : product.proteinsG != null ? `${product.proteinsG}g` : '—'}
      </td>

      {/* Lipides */}
      <td className="px-3 py-2 text-right text-sm text-content-muted w-20">
        {editing
          ? <EditCell value={product.fatsG} onCommit={(v) => onUpdate({ fatsG: n(v) })} className="text-right" />
          : product.fatsG != null ? `${product.fatsG}g` : '—'}
      </td>

      {/* Na */}
      <td className="px-3 py-2 text-right text-sm text-content-muted w-20">
        {editing
          ? <EditCell value={product.sodiumMg} onCommit={(v) => onUpdate({ sodiumMg: n(v) })} className="text-right" />
          : product.sodiumMg != null ? `${product.sodiumMg}mg` : '—'}
      </td>

      {/* Caféine */}
      <td className="px-3 py-2 text-right text-sm text-content-muted w-20">
        {editing
          ? <EditCell value={product.caffeineG != null ? product.caffeineG * 1000 : null} onCommit={(v) => onUpdate({ caffeineG: n(v) ? (n(v)! / 1000) : null })} className="text-right" />
          : product.caffeineG != null ? `${Math.round(product.caffeineG * 1000)}mg` : '—'}
      </td>

      {/* Poids */}
      {showWeight && (
        <td className="px-3 py-2 text-right text-sm text-content-muted w-16">
          {editing
            ? <EditCell value={product.weightG} onCommit={(v) => onUpdate({ weightG: n(v) })} className="text-right" />
            : product.weightG != null ? `${product.weightG}g` : '—'}
        </td>
      )}

      {/* Volume */}
      {showVolume && (
        <td className="px-3 py-2 text-right text-sm text-content-muted w-16">
          {editing
            ? <EditCell value={product.volumeML} onCommit={(v) => onUpdate({ volumeML: n(v) })} className="text-right" />
            : product.volumeML != null ? `${product.volumeML}ml` : '—'}
        </td>
      )}

      {/* Actions */}
      <td className="px-3 py-2 w-10 text-right">
        <button
          onClick={(e) => { e.stopPropagation(); onDelete(); }}
          className="p-1 rounded text-content-muted hover:text-red-400 hover:bg-red-900/20 transition-colors"
        >
          <Trash2 size={13} />
        </button>
      </td>
    </tr>
  );
}

// ─── Ligne de création ────────────────────────────────────────────────────────

function NewProductRow({
  onSave,
  showWeight,
  showVolume,
}: {
  onSave: (data: NutritionProductCreateRequest) => void;
  showWeight: boolean;
  showVolume: boolean;
}) {
  const { t } = useTranslation('race-plans');
  const [form, setForm] = useState<NutritionProductCreateRequest>(emptyNew());
  const [active, setActive] = useState(false);

  function patch(p: Partial<NutritionProductCreateRequest>) {
    setForm((f) => ({ ...f, ...p }));
  }

  function handleSave() {
    if (!form.name) return;
    onSave(form);
    setForm(emptyNew());
    setActive(false);
  }

  if (!active) {
    return (
      <tr className="border-t border-border/40">
        <td colSpan={9 + (showWeight ? 1 : 0) + (showVolume ? 1 : 0)} className="px-3 py-2">
          <button
            onClick={() => setActive(true)}
            className="flex items-center gap-1.5 text-sm text-content-muted hover:text-content transition-colors"
          >
            <Plus size={14} />
            {t('nutritionCatalogue.addProduct')}
          </button>
        </td>
      </tr>
    );
  }

  return (
    <tr className="border-t border-border/40 bg-surface-alt/40">
      <td className="px-3 py-2 w-28">
        <select
          value={form.type}
          onChange={(e) => patch({ type: e.target.value as NutritionProductType })}
          className="w-full bg-surface-alt border border-border/60 rounded px-1.5 py-0.5 text-xs text-content focus:outline-none focus:ring-1 focus:ring-cyan-500"
        >
          {TYPES.map((tp) => (
            <option key={tp} value={tp}>{t(`nutritionCatalogue.types.${tp}`)}</option>
          ))}
        </select>
      </td>
      <td className="px-3 py-2">
        <div className="flex gap-1">
          <input
            type="text"
            value={form.brand ?? ''}
            onChange={(e) => patch({ brand: e.target.value || null })}
            placeholder={t('nutritionCatalogue.brand')}
            className="w-24 bg-surface-alt border border-border/60 rounded px-1.5 py-0.5 text-xs text-content-muted focus:outline-none focus:ring-1 focus:ring-cyan-500"
          />
          <input
            type="text"
            value={form.name}
            onChange={(e) => patch({ name: e.target.value })}
            placeholder={t('nutritionCatalogue.name')}
            autoFocus
            className="flex-1 bg-surface-alt border border-border/60 rounded px-1.5 py-0.5 text-sm text-content focus:outline-none focus:ring-1 focus:ring-cyan-500"
          />
        </div>
      </td>
      {[
        { key: 'caloriesKcal', val: form.caloriesKcal ?? 0, onChange: (v: string) => patch({ caloriesKcal: num(v) }) },
        { key: 'carbsG', val: form.carbsG ?? 0, onChange: (v: string) => patch({ carbsG: num(v) }) },
        { key: 'proteinsG', val: form.proteinsG, onChange: (v: string) => patch({ proteinsG: n(v) }) },
        { key: 'fatsG', val: form.fatsG, onChange: (v: string) => patch({ fatsG: n(v) }) },
        { key: 'sodiumMg', val: form.sodiumMg, onChange: (v: string) => patch({ sodiumMg: n(v) }) },
        { key: 'caffeineG', val: form.caffeineG != null ? form.caffeineG * 1000 : null, onChange: (v: string) => patch({ caffeineG: n(v) ? n(v)! / 1000 : null }) },
      ].map(({ key, val, onChange }) => (
        <td key={key} className="px-3 py-2 w-20">
          <input
            type="number"
            value={val ?? ''}
            onChange={(e) => onChange(e.target.value)}
            className="w-full bg-surface-alt border border-border/60 rounded px-1.5 py-0.5 text-sm text-content text-right focus:outline-none focus:ring-1 focus:ring-cyan-500"
          />
        </td>
      ))}
      {showWeight && (
        <td className="px-3 py-2 w-16">
          <input type="number" value={form.weightG ?? ''} onChange={(e) => patch({ weightG: n(e.target.value) })}
            className="w-full bg-surface-alt border border-border/60 rounded px-1.5 py-0.5 text-sm text-content text-right focus:outline-none focus:ring-1 focus:ring-cyan-500" />
        </td>
      )}
      {showVolume && (
        <td className="px-3 py-2 w-16">
          <input type="number" value={form.volumeML ?? ''} onChange={(e) => patch({ volumeML: n(e.target.value) })}
            className="w-full bg-surface-alt border border-border/60 rounded px-1.5 py-0.5 text-sm text-content text-right focus:outline-none focus:ring-1 focus:ring-cyan-500" />
        </td>
      )}
      <td className="px-3 py-2 w-10">
        <button
          onClick={handleSave}
          disabled={!form.name}
          className="p-1 rounded bg-cyan-600 hover:bg-cyan-500 disabled:opacity-40 text-white transition-colors"
        >
          <Plus size={13} />
        </button>
      </td>
    </tr>
  );
}

// ─── Page principale ──────────────────────────────────────────────────────────

export default function NutritionCataloguePage() {
  const { t } = useTranslation('race-plans');
  const [typeFilter, setTypeFilter] = useState('');
  const [editingId, setEditingId] = useState<string | null>(null);
  // Buffer des modifications en cours (non encore sauvegardées)
  const pendingPatch = useRef<Partial<NutritionProductCreateRequest>>({});

  const { data: products, isLoading } = useNutritionProducts(typeFilter || undefined);
  const updateProduct = useUpdateNutritionProduct();
  const createProduct = useCreateNutritionProduct();
  const deleteProduct = useDeleteNutritionProduct();
  const importDefaults = useImportDefaultProducts();

  const showWeight = !!(products?.some((p) => p.weightG != null));
  const showVolume = !!(products?.some((p) => p.volumeML != null));

  const handleUpdate = useCallback((product: NutritionProduct, patch: Partial<NutritionProductCreateRequest>) => {
    pendingPatch.current = { ...pendingPatch.current, ...patch };
    // Save immediately (each field change triggers a save)
    const merged: NutritionProductCreateRequest = {
      name: product.name,
      brand: product.brand,
      type: product.type as NutritionProductType,
      caloriesKcal: product.caloriesKcal,
      carbsG: product.carbsG,
      proteinsG: product.proteinsG,
      fatsG: product.fatsG,
      sodiumMg: product.sodiumMg,
      caffeineG: product.caffeineG,
      weightG: product.weightG,
      volumeML: product.volumeML,
      notes: product.notes,
      ...pendingPatch.current,
    };
    updateProduct.mutate({ id: product.id, data: merged });
  }, [updateProduct]);

  function handleStartEdit(id: string) {
    pendingPatch.current = {};
    setEditingId(id);
  }

  async function handleDelete(product: NutritionProduct) {
    if (!confirm(t('nutritionCatalogue.confirmDelete'))) return;
    await deleteProduct.mutateAsync(product.id);
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold text-content tracking-tight">{t('nutritionCatalogue.title')}</h1>
          <p className="text-content-muted mt-1">{t('nutritionCatalogue.subtitle')}</p>
        </div>
        <div className="flex items-center gap-3">
          <select
            value={typeFilter}
            onChange={(e) => setTypeFilter(e.target.value)}
            className="bg-surface-card text-content border border-border rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500 appearance-none cursor-pointer"
          >
            <option value="">{t('nutritionCatalogue.allTypes')}</option>
            {TYPES.map((type) => (
              <option key={type} value={type}>{t(`nutritionCatalogue.types.${type}`)}</option>
            ))}
          </select>
        </div>
      </div>

      {/* Loading */}
      {isLoading && (
        <div className="flex items-center justify-center h-48">
          <div className="animate-spin rounded-full h-8 w-8 border-t-2 border-b-2 border-cyan-400" />
        </div>
      )}

      {/* Empty */}
      {!isLoading && (!products || products.length === 0) && (
        <div className="text-center py-16 space-y-4">
          <div className="text-5xl">🥗</div>
          <div>
            <p className="text-content font-medium">{t('nutritionCatalogue.emptyTitle')}</p>
            <p className="text-sm text-content-muted mt-1">{t('nutritionCatalogue.emptyHint')}</p>
          </div>
          <button
            onClick={() => importDefaults.mutate()}
            disabled={importDefaults.isPending}
            className="inline-flex items-center gap-2 bg-surface-card hover:bg-surface-alt border border-border hover:border-content-muted/30 text-content px-4 py-2 rounded-lg text-sm font-medium transition-colors disabled:opacity-50"
          >
            {importDefaults.isPending
              ? <span className="w-4 h-4 border-2 border-content-muted/30 border-t-content rounded-full animate-spin" />
              : <Download size={15} />}
            {t('nutritionCatalogue.importDefaults')}
          </button>
        </div>
      )}

      {/* Table */}
      {products && products.length > 0 && (
        <div className="bg-surface-card border border-border rounded-xl overflow-hidden">
          <div
            className="overflow-x-auto"
            onClick={() => setEditingId(null)}
          >
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-border bg-surface-alt/50 text-left text-xs text-content-muted uppercase tracking-wide">
                  <th className="px-3 py-2.5 w-28">{t('nutritionCatalogue.type')}</th>
                  <th className="px-3 py-2.5">{t('nutritionCatalogue.name')}</th>
                  <th className="px-3 py-2.5 text-right w-16">kcal</th>
                  <th className="px-3 py-2.5 text-right w-20">{t('nutritionCatalogue.carbs')}</th>
                  <th className="px-3 py-2.5 text-right w-20">{t('nutritionCatalogue.proteins')}</th>
                  <th className="px-3 py-2.5 text-right w-20">{t('nutritionCatalogue.fats')}</th>
                  <th className="px-3 py-2.5 text-right w-20">{t('nutritionCatalogue.sodium')}</th>
                  <th className="px-3 py-2.5 text-right w-20">{t('nutritionCatalogue.caffeine')}</th>
                  {showWeight && <th className="px-3 py-2.5 text-right w-16">{t('nutritionCatalogue.weight')}</th>}
                  {showVolume && <th className="px-3 py-2.5 text-right w-16">{t('nutritionCatalogue.volume')}</th>}
                  <th className="px-3 py-2.5 w-10" />
                </tr>
              </thead>
              <tbody>
                {products.map((product) => (
                  <ProductRow
                    key={product.id}
                    product={product}
                    editing={editingId === product.id}
                    onStartEdit={() => handleStartEdit(product.id)}
                    onUpdate={(patch) => handleUpdate(product, patch)}
                    onDelete={() => handleDelete(product)}
                    showWeight={showWeight}
                    showVolume={showVolume}
                  />
                ))}
                <NewProductRow
                  onSave={(data) => createProduct.mutate(data)}
                  showWeight={showWeight}
                  showVolume={showVolume}
                />
              </tbody>
            </table>
          </div>
          <div className="px-3 py-2 border-t border-border/40 text-xs text-content-muted">
            {t('nutritionCatalogue.productsCount', { count: products.length })}
            {' · '}
            {t('nutritionCatalogue.clickToEdit')}
            {' · '}
            <button
              onClick={() => importDefaults.mutate()}
              disabled={importDefaults.isPending}
              className="text-cyan-400 hover:text-cyan-300 transition-colors"
            >
              {t('nutritionCatalogue.importDefaults')}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
