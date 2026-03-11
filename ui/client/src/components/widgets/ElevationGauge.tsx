interface ElevationGaugeProps {
  gain: number;
  loss: number;
  label: string;
  unitLabel: string;
}

export default function ElevationGauge({
  gain,
  loss,
  label,
  unitLabel,
}: ElevationGaugeProps) {
  const radius = 40;
  const circumference = 2 * Math.PI * radius;
  const total = gain + loss;
  const gainPct = total > 0 ? gain / total : 0.5;
  const gainArc = gainPct * circumference;
  const lossArc = (1 - gainPct) * circumference;

  return (
    <div className="bg-surface-card rounded-2xl p-5 border border-border flex flex-col items-center">
      <div className="relative w-24 h-24 mb-3">
        <svg className="w-24 h-24 -rotate-90" viewBox="0 0 100 100">
          <circle cx="50" cy="50" r={radius} fill="none" stroke="#334155" strokeWidth="6" />
          {/* D+ green arc from top */}
          <circle
            cx="50" cy="50" r={radius} fill="none"
            stroke="#00ff88" strokeWidth="6"
            strokeDasharray={circumference} strokeDashoffset={circumference - gainArc}
            className="transition-all duration-1000"
          />
          {/* D- red arc continuing after green */}
          <circle
            cx="50" cy="50" r={radius} fill="none"
            stroke="#ff6b6b" strokeWidth="6"
            strokeDasharray={circumference} strokeDashoffset={circumference - lossArc}
            className="transition-all duration-1000"
            style={{ transform: `rotate(${gainPct * 360}deg)`, transformOrigin: '50px 50px' }}
          />
        </svg>
        <div className="absolute inset-0 flex flex-col items-center justify-center">
          <span className="text-sm font-bold text-accent-green leading-tight">+{Math.round(gain)}</span>
          <span className="text-sm font-bold text-accent-red leading-tight">&minus;{Math.round(loss)}</span>
        </div>
      </div>
      <p className="text-xs text-content-muted">{unitLabel}</p>
      <p className="text-sm font-medium text-content mt-1">{label}</p>
    </div>
  );
}
