import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  Cell,
} from 'recharts';
import { Mountain } from 'lucide-react';
import type { GpxStats } from '../../types/activity';
import {
  TOOLTIP_STYLE,
  AXIS_TICK,
  AXIS_LINE,
  GRID_PROPS,
} from '../../constants/chart-theme';

interface ElevationChartProps {
  stats: GpxStats;
}

interface ElevationBarEntry {
  name: string;
  value: number;
  color: string;
}

export default function ElevationChart({ stats }: ElevationChartProps) {
  const data: ElevationBarEntry[] = [
    { name: 'Min Elev.', value: Math.round(stats.min_elevation_m), color: 'var(--accent)' },
    { name: 'Max Elev.', value: Math.round(stats.max_elevation_m), color: 'var(--accent-green)' },
    { name: 'Gain', value: Math.round(stats.elevation_gain_m), color: 'var(--accent-orange)' },
    { name: 'Loss', value: Math.round(stats.elevation_loss_m), color: 'var(--accent-red)' },
  ];

  return (
    <div className="flex flex-col gap-3">
      {/* Header */}
      <div className="flex items-center gap-2">
        <Mountain size={18} className="text-accent" />
        <h4 className="text-sm font-semibold text-content">Elevation Profile</h4>
      </div>

      {/* Summary chips */}
      <div className="flex flex-wrap gap-2">
        <ElevationChip label="Min" value={`${Math.round(stats.min_elevation_m)} m`} color="var(--accent)" />
        <ElevationChip label="Max" value={`${Math.round(stats.max_elevation_m)} m`} color="var(--accent-green)" />
        <ElevationChip label="Gain" value={`+${Math.round(stats.elevation_gain_m)} m`} color="var(--accent-orange)" />
        <ElevationChip label="Loss" value={`-${Math.round(stats.elevation_loss_m)} m`} color="var(--accent-red)" />
      </div>

      {/* Bar chart */}
      <div className="bg-surface-card rounded-xl border border-border p-4">
        <ResponsiveContainer width="100%" height={220}>
          <BarChart data={data} margin={{ top: 8, right: 8, bottom: 4, left: 0 }}>
            <CartesianGrid {...GRID_PROPS} />
            <XAxis
              dataKey="name"
              tick={AXIS_TICK}
              axisLine={AXIS_LINE}
              tickLine={false}
            />
            <YAxis
              tick={AXIS_TICK}
              axisLine={false}
              tickLine={false}
              width={50}
              tickFormatter={(v: number) => `${v} m`}
            />
            <Tooltip
              contentStyle={TOOLTIP_STYLE}
              cursor={{ fill: 'var(--chart-cursor)' }}
              formatter={(value) => [`${Number(value) || 0} m`, 'Elevation']}
            />
            <Bar dataKey="value" radius={[6, 6, 0, 0]} maxBarSize={48}>
              {data.map((entry, index) => (
                <Cell key={index} fill={entry.color} fillOpacity={0.85} />
              ))}
            </Bar>
          </BarChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
}

function ElevationChip({
  label,
  value,
  color,
}: {
  label: string;
  value: string;
  color: string;
}) {
  return (
    <div
      className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg text-xs font-medium border"
      style={{
        color,
        backgroundColor: `${color}12`,
        borderColor: `${color}25`,
      }}
    >
      <span className="text-content-muted">{label}</span>
      <span>{value}</span>
    </div>
  );
}
