interface RadialGaugeProps {
  value: number;
  max: number;
  label: string;
  unit: string;
  color: string;
  size?: number;
}

export default function RadialGauge({
  value,
  max,
  label,
  unit,
  color,
  size = 120,
}: RadialGaugeProps) {
  const strokeWidth = 8;
  const radius = (size - strokeWidth) / 2;
  const circumference = 2 * Math.PI * radius;
  const clampedValue = Math.min(Math.max(value, 0), max);
  const progress = max > 0 ? clampedValue / max : 0;
  const strokeDashoffset = circumference * (1 - progress);
  const center = size / 2;

  // Format value for display
  const displayValue =
    value >= 1000 ? `${(value / 1000).toFixed(1)}k` : Number.isInteger(value) ? String(value) : value.toFixed(1);

  return (
    <div className="flex flex-col items-center gap-1">
      <svg
        width={size}
        height={size}
        viewBox={`0 0 ${size} ${size}`}
        className="transform -rotate-90"
      >
        {/* Background track */}
        <circle
          cx={center}
          cy={center}
          r={radius}
          fill="none"
          stroke="rgba(255,255,255,0.06)"
          strokeWidth={strokeWidth}
        />
        {/* Progress arc */}
        <circle
          cx={center}
          cy={center}
          r={radius}
          fill="none"
          stroke={color}
          strokeWidth={strokeWidth}
          strokeDasharray={circumference}
          strokeDashoffset={strokeDashoffset}
          strokeLinecap="round"
          className="transition-all duration-700 ease-out"
        />
        {/* Center text group (counter-rotate to keep text upright) */}
        <g transform={`rotate(90, ${center}, ${center})`}>
          <text
            x={center}
            y={center - 4}
            textAnchor="middle"
            dominantBaseline="central"
            className="fill-white font-bold"
            style={{ fontSize: size * 0.2 }}
          >
            {displayValue}
          </text>
          <text
            x={center}
            y={center + size * 0.15}
            textAnchor="middle"
            dominantBaseline="central"
            className="fill-[#a0a0b0]"
            style={{ fontSize: size * 0.1 }}
          >
            {unit}
          </text>
        </g>
      </svg>
      <span className="text-xs text-[#a0a0b0] font-medium tracking-wide">
        {label}
      </span>
    </div>
  );
}
