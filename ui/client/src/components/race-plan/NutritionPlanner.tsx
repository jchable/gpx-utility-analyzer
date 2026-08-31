import { useState, useMemo, useCallback, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { useQuery } from '@tanstack/react-query';
import {
  BarChart, Bar, XAxis, YAxis, Tooltip, Legend, ResponsiveContainer, ReferenceLine,
} from 'recharts';
import { Flame, Droplets, Plus, Trash2, ChevronDown, ChevronRight } from 'lucide-react';
import { api } from '../../api/client';
import {
  useNutritionProducts,
  useAddNutritionItem,
  useDeleteNutritionItem,
  useUpdateRacePlan,
} from '../../hooks/useRacePlans';
import { toRacePlanUpdateRequest } from '../../types/race-plan';
import type {
  RacePlanDetail,
  RacePlanCheckpoint,
  RacePlanNutritionItem,
  NutritionProduct,
} from '../../types/race-plan';

// ─── Constantes ──────────────────────────────────────────────────────────────

const MET_KCAL_PER_KG_PER_H = 4.0;
const DEFAULT_WEIGHT_KG = 70;
const DEFAULT_SWEAT_ML_PER_H = 500;

// ─── Helpers ─────────────────────────────────────────────────────────────────

function formatTime(seconds: number | null): string {
  if (seconds == null) return '—';
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  return `H+${h}:${String(m).padStart(2, '0')}`;
}

function waterFromItem(item: RacePlanNutritionItem, products: NutritionProduct[]): number {
  const prod = item.productId ? products.find((p) => p.id === item.productId) : null;
  if (item.unit === 'ml') return item.quantity;
  if (item.unit === 'unit' && prod?.volumeML) return item.quantity * prod.volumeML;
  return 0;
}

function kcalFromItem(item: RacePlanNutritionItem): number {
  if (item.caloriesKcal == null) return 0;
  return item.caloriesKcal * item.quantity;
}

// ─── Types internes ───────────────────────────────────────────────────────────

interface SegmentCalc {
  from: RacePlanCheckpoint;
  to: RacePlanCheckpoint;
  durationH: number;
  needKcal: number;
  needWaterML: number;
  segItems: RacePlanNutritionItem[];
  atItems: RacePlanNutritionItem[];
  plannedKcal: number;
}

function buildSegments(
  checkpoints: RacePlanCheckpoint[],
  items: RacePlanNutritionItem[],
  weightKg: number,
  sweatRate: number,
): SegmentCalc[] {
  const sorted = [...checkpoints].sort((a, b) => a.order - b.order);
  return sorted.slice(0, -1).map((from, i) => {
    const to = sorted[i + 1];
    const durationSec = Math.max(0,
      (to.targetArrivalSeconds ?? 0) - (from.targetArrivalSeconds ?? 0) - (from.plannedPauseSeconds ?? 0),
    );
    const durationH = durationSec / 3600;
    const segItems = items.filter((it) => it.fromCheckpointId === from.id && it.toCheckpointId === to.id);
    const atItems = items.filter((it) => it.atCheckpointId === to.id);
    const plannedKcal = [...segItems, ...atItems].reduce((s, it) => s + kcalFromItem(it), 0);
    return {
      from, to, durationH,
      needKcal: durationH * weightKg * MET_KCAL_PER_KG_PER_H,
      needWaterML: durationH * sweatRate,
      segItems, atItems, plannedKcal,
    };
  });
}

// ─── Formulaire ajout item ────────────────────────────────────────────────────

function AddItemForm({ products, onAdd, onCancel }: {
  products: NutritionProduct[];
  onAdd: (productId: string | null, productName: string, quantity: number, unit: string) => void;
  onCancel: () => void;
}) {
  const { t } = useTranslation('race-plans');
  const [productId, setProductId] = useState('');
  const [customName, setCustomName] = useState('');
  const [quantity, setQuantity] = useState(1);
  const [unit, setUnit] = useState('unit');
  const selectedProduct = products.find((p) => p.id === productId);

  function handleSubmit() {
    const name = selectedProduct?.name ?? customName;
    if (!name) return;
    onAdd(productId || null, name, quantity, unit);
  }

  return (
    <div className="mt-2 p-3 bg-surface-alt/60 rounded-lg border border-border/60 flex flex-wrap gap-2 items-end">
      <div className="flex-1 min-w-40">
        <label className="text-xs text-content-muted block mb-1">{t('nutritionCatalogue.name')}</label>
        <select
          value={productId}
          onChange={(e) => setProductId(e.target.value)}
          className="w-full bg-surface-card border border-border rounded px-2 py-1.5 text-sm text-content focus:outline-none focus:ring-1 focus:ring-cyan-500"
        >
          <option value="">{t('nutrition.customProduct')}</option>
          {products.map((p) => (
            <option key={p.id} value={p.id}>{p.brand ? `${p.brand} — ${p.name}` : p.name}</option>
          ))}
        </select>
      </div>
      {!productId && (
        <div className="flex-1 min-w-32">
          <label className="text-xs text-content-muted block mb-1">{t('nutrition.productName')}</label>
          <input type="text" value={customName} onChange={(e) => setCustomName(e.target.value)} placeholder={t('nutrition.customProductPlaceholder')}
            className="w-full bg-surface-card border border-border rounded px-2 py-1.5 text-sm text-content focus:outline-none focus:ring-1 focus:ring-cyan-500" />
        </div>
      )}
      <div className="w-20">
        <label className="text-xs text-content-muted block mb-1">{t('nutrition.qty')}</label>
        <input type="number" min={0.5} step={0.5} value={quantity} onChange={(e) => setQuantity(parseFloat(e.target.value) || 1)}
          className="w-full bg-surface-card border border-border rounded px-2 py-1.5 text-sm text-content text-right focus:outline-none focus:ring-1 focus:ring-cyan-500" />
      </div>
      <div className="w-20">
        <label className="text-xs text-content-muted block mb-1">{t('nutrition.unit')}</label>
        <select value={unit} onChange={(e) => setUnit(e.target.value)}
          className="w-full bg-surface-card border border-border rounded px-2 py-1.5 text-sm text-content focus:outline-none focus:ring-1 focus:ring-cyan-500">
          <option value="unit">{t('nutrition.unitUnit')}</option>
          <option value="ml">ml</option>
          <option value="g">g</option>
        </select>
      </div>
      <div className="flex gap-2">
        <button onClick={handleSubmit} disabled={!productId && !customName}
          className="px-3 py-1.5 rounded bg-cyan-600 hover:bg-cyan-500 disabled:opacity-40 text-white text-sm transition-colors">
          {t('nutrition.add')}
        </button>
        <button onClick={onCancel}
          className="px-3 py-1.5 rounded border border-border text-content-muted hover:text-content text-sm transition-colors">✕</button>
      </div>
    </div>
  );
}

// ─── Bloc segment ─────────────────────────────────────────────────────────────

function SegmentBlock({ seg, products, onAddSegItem, onAddAtItem, onDelete }: {
  seg: SegmentCalc;
  products: NutritionProduct[];
  onAddSegItem: (fromId: string, toId: string, pid: string | null, name: string, qty: number, unit: string) => void;
  onAddAtItem: (checkpointId: string, pid: string | null, name: string, qty: number, unit: string) => void;
  onDelete: (id: string) => void;
}) {
  const { t } = useTranslation('race-plans');
  const [open, setOpen] = useState(true);
  const [showAddSeg, setShowAddSeg] = useState(false);
  const [showAddAt, setShowAddAt] = useState(false);

  const allItems = [...seg.segItems, ...seg.atItems];
  const plannedWater = allItems.reduce((s, it) => s + waterFromItem(it, products), 0);
  const needKcalRounded = Math.round(seg.needKcal);
  const pctKcal = needKcalRounded > 0 ? Math.min(100, (seg.plannedKcal / needKcalRounded) * 100) : 0;

  function itemRow(item: RacePlanNutritionItem) {
    const prod = item.productId ? products.find((p) => p.id === item.productId) : null;
    const water = waterFromItem(item, products);
    return (
      <tr key={item.id} className="border-b border-border/20 text-sm">
        <td className="py-1.5 pr-3">
          {prod && <span className="text-xs text-content-muted bg-surface-alt px-1.5 py-0.5 rounded mr-2">{t(`nutritionCatalogue.types.${prod.type}`)}</span>}
          <span className="text-content">{item.productName}</span>
        </td>
        <td className="py-1.5 pr-3 text-right text-content-muted w-16">{item.quantity} {item.unit}</td>
        <td className="py-1.5 pr-3 text-right text-orange-400 font-medium w-16">{Math.round(kcalFromItem(item))}</td>
        <td className="py-1.5 pr-3 text-right text-blue-400 w-16">{water > 0 ? `${water}ml` : '—'}</td>
        <td className="py-1.5 w-8 text-right">
          <button onClick={() => onDelete(item.id)} className="p-1 rounded text-content-muted hover:text-red-400 transition-colors"><Trash2 size={12} /></button>
        </td>
      </tr>
    );
  }

  return (
    <div className="bg-surface-card border border-border rounded-xl overflow-hidden">
      {/* Header */}
      <div className="flex items-center justify-between px-4 py-3 cursor-pointer select-none hover:bg-surface-alt/30 transition-colors" onClick={() => setOpen((o) => !o)}>
        <div className="flex items-center gap-3 min-w-0">
          {open ? <ChevronDown size={16} className="text-content-muted shrink-0" /> : <ChevronRight size={16} className="text-content-muted shrink-0" />}
          <div className="min-w-0">
            <p className="font-semibold text-content truncate">{seg.from.name} → {seg.to.name}</p>
            <p className="text-xs text-content-muted">
              {formatTime(seg.from.targetArrivalSeconds)} → {formatTime(seg.to.targetArrivalSeconds)}
              {' · '}{((seg.to.distanceKm ?? 0) - (seg.from.distanceKm ?? 0)).toFixed(1)} km
            </p>
          </div>
        </div>
        <div className="flex items-center gap-4 shrink-0 ml-4">
          <div className="text-right hidden sm:block">
            <p className="text-xs text-content-muted">{t('nutrition.need')}</p>
            <p className="text-sm font-bold">
              <span className="text-orange-400">{needKcalRounded} kcal</span>
              <span className="mx-1 text-content-muted">/</span>
              <span className="text-blue-400">{(seg.needWaterML / 1000).toFixed(1)} L</span>
            </p>
          </div>
          <div className="text-right hidden sm:block">
            <p className="text-xs text-content-muted">{t('nutrition.planned')}</p>
            <p className="text-sm font-bold">
              <span className={seg.plannedKcal >= needKcalRounded * 0.8 ? 'text-green-400' : 'text-yellow-400'}>{Math.round(seg.plannedKcal)} kcal</span>
              <span className="mx-1 text-content-muted">/</span>
              <span className="text-blue-300">{(plannedWater / 1000).toFixed(1)} L</span>
            </p>
          </div>
          <div className="w-16 h-1.5 bg-surface-alt rounded-full overflow-hidden hidden sm:block">
            <div className={`h-full rounded-full ${pctKcal >= 80 ? 'bg-green-500' : pctKcal >= 50 ? 'bg-yellow-500' : 'bg-red-500'}`} style={{ width: `${pctKcal}%` }} />
          </div>
        </div>
      </div>

      {open && (
        <div className="px-4 pb-4 border-t border-border/40 space-y-3">
          {/* En route */}
          <div className="mt-3">
            <p className="text-xs font-medium text-content-muted uppercase tracking-wide mb-1">{t('nutrition.enRoute')}</p>
            {seg.segItems.length > 0 && (
              <table className="w-full"><tbody>{seg.segItems.map(itemRow)}</tbody></table>
            )}
            {showAddSeg
              ? <AddItemForm products={products} onAdd={(pid, name, qty, unit) => { onAddSegItem(seg.from.id, seg.to.id, pid, name, qty, unit); setShowAddSeg(false); }} onCancel={() => setShowAddSeg(false)} />
              : <button onClick={() => setShowAddSeg(true)} className="mt-1.5 flex items-center gap-1.5 text-xs text-content-muted hover:text-content transition-colors"><Plus size={12} />{t('nutrition.addToSegment')}</button>
            }
          </div>

          {/* Au ravito */}
          <div className="pt-3 border-t border-border/30">
            <p className="text-xs font-medium text-content-muted uppercase tracking-wide mb-1">
              {t('nutrition.atAidStation')} : {seg.to.name}
              {seg.to.plannedPauseSeconds ? ` — ${Math.round(seg.to.plannedPauseSeconds / 60)} min` : ''}
            </p>
            {seg.atItems.length > 0 && (
              <table className="w-full"><tbody>{seg.atItems.map(itemRow)}</tbody></table>
            )}
            {showAddAt
              ? <AddItemForm products={products} onAdd={(pid, name, qty, unit) => { onAddAtItem(seg.to.id, pid, name, qty, unit); setShowAddAt(false); }} onCancel={() => setShowAddAt(false)} />
              : <button onClick={() => setShowAddAt(true)} className="mt-1.5 flex items-center gap-1.5 text-xs text-content-muted hover:text-content transition-colors"><Plus size={12} />{t('nutrition.addAtCheckpoint')}</button>
            }
          </div>
        </div>
      )}
    </div>
  );
}

// ─── Graphique balance ────────────────────────────────────────────────────────

function BalanceChart({ segs, products }: { segs: SegmentCalc[]; products: NutritionProduct[] }) {
  const { t } = useTranslation('race-plans');

  const data = segs.map((seg) => ({
    name: `${seg.from.name.slice(0, 10)}…`,
    [t('nutrition.need')]: Math.round(seg.needKcal),
    [t('nutrition.planned')]: Math.round(seg.plannedKcal),
  }));

  const totalNeed = segs.reduce((s, seg) => s + seg.needKcal, 0);
  const totalPlanned = segs.reduce((s, seg) => s + seg.plannedKcal, 0);
  const totalWater = segs.reduce((s, seg) =>
    s + [...seg.segItems, ...seg.atItems].reduce((ss, it) => ss + waterFromItem(it, products), 0), 0);
  const totalFoodG = segs.reduce((s, seg) =>
    s + [...seg.segItems, ...seg.atItems].reduce((ss, it) => {
      const prod = it.productId ? products.find((p) => p.id === it.productId) : null;
      if (it.unit === 'g') return ss + it.quantity;
      if (it.unit === 'unit' && prod?.weightG) return ss + it.quantity * prod.weightG;
      return ss;
    }, 0), 0);
  const totalCarbs = segs.reduce((s, seg) =>
    s + [...seg.segItems, ...seg.atItems].reduce((ss, it) => {
      if (it.carbsG == null) return ss;
      return ss + it.carbsG * it.quantity;
    }, 0), 0);
  const deficit = Math.round(totalPlanned - totalNeed);

  return (
    <div className="bg-surface-card border border-border rounded-xl p-4 space-y-4">
      <div className="flex flex-wrap items-center gap-4">
        <h3 className="font-semibold text-content">{t('nutrition.balance')}</h3>
        <div className="flex flex-wrap gap-3 text-sm">
          {totalFoodG > 0 && (
            <span className="text-content-muted">{t('nutrition.totalFoodWeight')}: <span className="text-content font-medium">{Math.round(totalFoodG)} g</span></span>
          )}
          {totalWater > 0 && (
            <span className="text-content-muted"><Droplets size={13} className="inline mr-1 text-blue-400" />{(totalWater / 1000).toFixed(1)} L</span>
          )}
          <span className={`font-semibold px-2 py-0.5 rounded-full text-xs ${deficit >= 0 ? 'bg-green-900/30 text-green-400' : 'bg-red-900/30 text-red-400'}`}>
            {deficit >= 0 ? t('nutrition.surplus') : t('nutrition.deficit')}: {deficit >= 0 ? '+' : ''}{deficit} kcal
          </span>
        </div>
      </div>
      <ResponsiveContainer width="100%" height={200}>
        <BarChart data={data} margin={{ top: 0, right: 0, left: -20, bottom: 0 }}>
          <XAxis dataKey="name" tick={{ fontSize: 10, fill: '#6b7280' }} />
          <YAxis tick={{ fontSize: 10, fill: '#6b7280' }} />
          <Tooltip contentStyle={{ backgroundColor: '#1a1a2e', border: '1px solid #2d2d44', borderRadius: 8, fontSize: 12 }} labelStyle={{ color: '#e2e8f0' }} />
          <Legend wrapperStyle={{ fontSize: 12 }} />
          <ReferenceLine y={0} stroke="#374151" />
          <Bar dataKey={t('nutrition.need')} fill="#f97316" radius={[3, 3, 0, 0]} />
          <Bar dataKey={t('nutrition.planned')} fill="#22c55e" radius={[3, 3, 0, 0]} />
        </BarChart>
      </ResponsiveContainer>
      {totalCarbs > 0 && (
        <p className="text-xs text-content-muted">
          {t('nutrition.macroBreakdown')}: <span className="text-yellow-400">{t('nutritionCatalogue.carbs')} {Math.round(totalCarbs)}g</span>
        </p>
      )}
    </div>
  );
}

// ─── Composant principal ──────────────────────────────────────────────────────

export default function NutritionPlanner({ plan }: { plan: RacePlanDetail }) {
  const { t } = useTranslation('race-plans');
  const addItem = useAddNutritionItem();
  const deleteItem = useDeleteNutritionItem();
  const updatePlan = useUpdateRacePlan();
  const { data: products = [] } = useNutritionProducts();
  const { data: userProfile } = useQuery({
    queryKey: ['profile'],
    queryFn: () => api.getUserProfile(),
    staleTime: 10 * 60_000,
  });

  const weightKg = userProfile?.weightKg ?? DEFAULT_WEIGHT_KG;
  const sweatRate = plan.sweatRateMLPerHour ?? DEFAULT_SWEAT_ML_PER_H;

  const sweatDebounce = useRef<ReturnType<typeof setTimeout> | null>(null);
  function handleSweatRateChange(val: number) {
    if (sweatDebounce.current) clearTimeout(sweatDebounce.current);
    sweatDebounce.current = setTimeout(() => {
      updatePlan.mutate({
        id: plan.id,
        data: toRacePlanUpdateRequest(plan, { sweatRateMLPerHour: val }),
      });
    }, 800);
  }

  const segs = useMemo(
    () => buildSegments(plan.checkpoints, plan.nutritionItems, weightKg, sweatRate),
    [plan.checkpoints, plan.nutritionItems, weightKg, sweatRate],
  );

  const handleAddSegItem = useCallback((fromId: string, toId: string, pid: string | null, name: string, qty: number, unit: string) => {
    addItem.mutate({ planId: plan.id, data: { fromCheckpointId: fromId, toCheckpointId: toId, productId: pid ?? undefined, productName: name, quantity: qty, unit: unit as 'unit' | 'ml' | 'g' } });
  }, [addItem, plan.id]);

  const handleAddAtItem = useCallback((checkpointId: string, pid: string | null, name: string, qty: number, unit: string) => {
    addItem.mutate({ planId: plan.id, data: { atCheckpointId: checkpointId, productId: pid ?? undefined, productName: name, quantity: qty, unit: unit as 'unit' | 'ml' | 'g' } });
  }, [addItem, plan.id]);

  const handleDelete = useCallback((id: string) => { deleteItem.mutate({ planId: plan.id, itemId: id }); }, [deleteItem, plan.id]);

  if (plan.checkpoints.length < 2) {
    return <div className="text-center py-16 text-content-muted"><p>{t('nutrition.noCheckpoints')}</p></div>;
  }

  return (
    <div className="space-y-4">
      {/* Taux de transpiration */}
      <div className="bg-surface-card border border-border rounded-xl px-4 py-3 flex flex-wrap items-center gap-4">
        <div className="flex items-center gap-2 text-sm">
          <Droplets size={16} className="text-blue-400" />
          <span className="text-content-muted">{t('nutrition.sweatRate')}</span>
        </div>
        <div className="flex items-center gap-2">
          <input type="number" min={100} max={2000} step={50} defaultValue={sweatRate}
            onChange={(e) => handleSweatRateChange(parseFloat(e.target.value) || DEFAULT_SWEAT_ML_PER_H)}
            className="w-20 bg-surface-alt border border-border rounded px-2 py-1 text-sm text-content text-right focus:outline-none focus:ring-1 focus:ring-blue-500" />
          <span className="text-content-muted text-sm">{t('nutrition.sweatRateUnit')}</span>
        </div>
        {!userProfile?.weightKg && (
          <p className="text-xs text-yellow-500">
            <Flame size={12} className="inline mr-1" />
            {t('nutrition.weightFallback', { weight: DEFAULT_WEIGHT_KG })}
          </p>
        )}
        <p className="text-xs text-content-muted ml-auto hidden sm:block">{t('nutrition.sweatRateHint')}</p>
      </div>

      {/* Segments */}
      <div className="space-y-3">
        {segs.map((seg) => (
          <SegmentBlock key={`${seg.from.id}-${seg.to.id}`} seg={seg} products={products}
            onAddSegItem={handleAddSegItem} onAddAtItem={handleAddAtItem} onDelete={handleDelete} />
        ))}
      </div>

      {/* Balance */}
      {segs.length > 0 && <BalanceChart segs={segs} products={products} />}
    </div>
  );
}
