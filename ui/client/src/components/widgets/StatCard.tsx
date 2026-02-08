import type { ReactNode } from 'react';

interface StatCardProps {
  label: string;
  value: string | number;
  unit?: string;
  icon?: ReactNode;
  color?: string;
}

export default function StatCard({ label, value, unit, icon, color = '#00d4ff' }: StatCardProps) {
  return (
    <div className="flex items-center gap-4 bg-[#16213e] rounded-xl border border-white/5 px-4 py-3 transition-colors hover:border-white/10">
      {icon && (
        <div
          className="flex items-center justify-center w-10 h-10 rounded-lg shrink-0"
          style={{ backgroundColor: `${color}15` }}
        >
          <span style={{ color }}>{icon}</span>
        </div>
      )}
      <div className="flex flex-col min-w-0">
        <span className="text-xs text-[#a0a0b0] font-medium tracking-wide truncate">
          {label}
        </span>
        <div className="flex items-baseline gap-1">
          <span className="text-lg font-bold text-white leading-tight">
            {value}
          </span>
          {unit && (
            <span className="text-xs text-[#a0a0b0] font-medium">{unit}</span>
          )}
        </div>
      </div>
    </div>
  );
}
