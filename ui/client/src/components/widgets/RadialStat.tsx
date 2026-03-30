interface RadialStatProps {
  label: string;
  value: string;
  unit: string;
  percentage: number;
  color: string;
  className?: string;
}

export default function RadialStat({
  label,
  value,
  unit,
  percentage,
  color,
  className,
}: RadialStatProps) {
  const radius = 40;
  const circumference = 2 * Math.PI * radius;
  const offset = circumference - (Math.min(percentage, 100) / 100) * circumference;

  return (
    <div className={`bg-surface-card rounded-2xl p-5 border border-border flex flex-col items-center justify-center${className ? ` ${className}` : ''}`}>
      <div className="relative w-24 h-24 mb-3">
        <svg className="w-24 h-24 -rotate-90" viewBox="0 0 100 100">
          <circle cx="50" cy="50" r={radius} fill="none" stroke="var(--ring-track)" strokeWidth="6" />
          <circle
            cx="50" cy="50" r={radius} fill="none"
            stroke={color} strokeWidth="6" strokeLinecap="round"
            strokeDasharray={circumference} strokeDashoffset={offset}
            className="transition-all duration-1000"
          />
        </svg>
        <div className="absolute inset-0 flex items-center justify-center">
          <span className="text-lg font-bold text-content">{value}</span>
        </div>
      </div>
      <p className="text-xs text-content-muted">{unit}</p>
      <p className="text-sm font-medium text-content mt-1">{label}</p>
    </div>
  );
}
