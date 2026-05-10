import { useTranslation } from 'react-i18next';
import { Gauge } from 'lucide-react';

interface Props {
  value: number;
  onChange: (value: number) => void;
  disabled?: boolean;
}

const PRESETS = [
  { label: 'Elite', value: 0.95, color: 'text-purple-400' },
  { label: 'Fast', value: 0.85, color: 'text-cyan-400' },
  { label: 'Medium', value: 0.75, color: 'text-green-400' },
  { label: 'Conservative', value: 0.65, color: 'text-amber-400' },
  { label: 'Hiker', value: 0.50, color: 'text-orange-400' },
];

function getColor(value: number): string {
  if (value >= 0.9) return '#a855f7';
  if (value >= 0.8) return '#06b6d4';
  if (value >= 0.7) return '#22c55e';
  if (value >= 0.6) return '#f59e0b';
  return '#f97316';
}

export default function PerformanceCoefficientSlider({ value, onChange, disabled }: Props) {
  const { t } = useTranslation('race-plans');
  const pct = Math.round(value * 100);
  const color = getColor(value);

  return (
    <div className="bg-surface-card border border-border rounded-xl p-4 space-y-3">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Gauge size={16} className="text-content-muted" />
          <span className="text-sm font-medium text-content">{t('timing.performance')}</span>
        </div>
        <span className="text-lg font-bold" style={{ color }}>{pct}%</span>
      </div>

      <div className="relative">
        <input
          type="range"
          min={30}
          max={100}
          step={1}
          value={pct}
          disabled={disabled}
          onChange={(e) => onChange(Number(e.target.value) / 100)}
          className="w-full h-2 rounded-full appearance-none cursor-pointer bg-surface-alt"
          style={{
            background: `linear-gradient(to right, ${color} 0%, ${color} ${((pct - 30) / 70) * 100}%, var(--surface-alt) ${((pct - 30) / 70) * 100}%)`,
          }}
        />
      </div>

      <div className="text-xs text-content-muted text-center">
        {t('timing.toblerRatio')}
      </div>

      {/* Preset buttons */}
      <div className="flex gap-1.5 flex-wrap">
        {PRESETS.map((p) => (
          <button
            key={p.label}
            onClick={() => onChange(p.value)}
            disabled={disabled}
            className={`text-xs px-2 py-1 rounded-md border transition-colors ${
              Math.abs(value - p.value) < 0.01
                ? 'border-transparent bg-accent/20 text-accent'
                : 'border-border text-content-muted hover:text-content hover:bg-surface-alt/50'
            }`}
          >
            {p.label}
          </button>
        ))}
      </div>
    </div>
  );
}
