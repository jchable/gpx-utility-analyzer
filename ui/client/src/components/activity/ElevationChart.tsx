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
    { name: 'Min Elev.', value: Math.round(stats.min_elevation_m), color: '#00d4ff' },
    { name: 'Max Elev.', value: Math.round(stats.max_elevation_m), color: '#00ff88' },
    { name: 'Gain', value: Math.round(stats.elevation_gain_m), color: '#ff8800' },
    { name: 'Loss', value: Math.round(stats.elevation_loss_m), color: '#ff4444' },
  ];

  return (
    <div className="flex flex-col gap-3">
      {/* Header */}
      <div className="flex items-center gap-2">
        <Mountain size={18} className="text-[#00d4ff]" />
        <h4 className="text-sm font-semibold text-white">Elevation Profile</h4>
      </div>

      {/* Summary chips */}
      <div className="flex flex-wrap gap-2">
        <ElevationChip label="Min" value={`${Math.round(stats.min_elevation_m)} m`} color="#00d4ff" />
        <ElevationChip label="Max" value={`${Math.round(stats.max_elevation_m)} m`} color="#00ff88" />
        <ElevationChip label="Gain" value={`+${Math.round(stats.elevation_gain_m)} m`} color="#ff8800" />
        <ElevationChip label="Loss" value={`-${Math.round(stats.elevation_loss_m)} m`} color="#ff4444" />
      </div>

      {/* Bar chart */}
      <div className="bg-[#16213e] rounded-xl border border-white/5 p-4">
        <ResponsiveContainer width="100%" height={220}>
          <BarChart data={data} margin={{ top: 8, right: 8, bottom: 4, left: 0 }}>
            <CartesianGrid
              strokeDasharray="3 3"
              stroke="rgba(255,255,255,0.05)"
              vertical={false}
            />
            <XAxis
              dataKey="name"
              tick={{ fill: '#a0a0b0', fontSize: 11 }}
              axisLine={{ stroke: 'rgba(255,255,255,0.08)' }}
              tickLine={false}
            />
            <YAxis
              tick={{ fill: '#a0a0b0', fontSize: 11 }}
              axisLine={false}
              tickLine={false}
              width={50}
              tickFormatter={(v: number) => `${v} m`}
            />
            <Tooltip
              contentStyle={{
                backgroundColor: '#0f0f1a',
                border: '1px solid rgba(255,255,255,0.1)',
                borderRadius: '8px',
                color: '#e0e0e0',
                fontSize: '12px',
              }}
              cursor={{ fill: 'rgba(255,255,255,0.03)' }}
              formatter={(value: number | undefined) => [`${value ?? 0} m`, 'Elevation']}
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
      <span className="text-[#a0a0b0]">{label}</span>
      <span>{value}</span>
    </div>
  );
}
