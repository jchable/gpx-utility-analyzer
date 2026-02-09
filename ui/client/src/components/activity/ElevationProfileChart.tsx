import { useState, useMemo } from 'react';
import {
  ComposedChart,
  Area,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
} from 'recharts';
import { Mountain } from 'lucide-react';
import type { ProfilePoint } from '../../utils/gpx';

interface ElevationProfileChartProps {
  data: ProfilePoint[];
  loading?: boolean;
}

const COLORS = {
  elevation: '#00d4ff',
  speed: '#ff8800',
  gap: '#00ff88',
  text: '#a0a0b0',
  grid: 'rgba(255,255,255,0.05)',
  axisLine: 'rgba(255,255,255,0.08)',
  tooltipBg: '#0f0f1a',
} as const;

export default function ElevationProfileChart({ data, loading }: ElevationProfileChartProps) {
  const [showSpeed, setShowSpeed] = useState(true);
  const [showGap, setShowGap] = useState(true);

  const hasSpeed = useMemo(() => data.some((d) => d.speed > 0), [data]);

  const elevDomain = useMemo(() => {
    if (data.length === 0) return [0, 100];
    const eles = data.map((d) => d.elevation);
    const min = Math.min(...eles);
    const max = Math.max(...eles);
    const padding = Math.max((max - min) * 0.05, 10);
    return [Math.floor(min - padding), Math.ceil(max + padding)];
  }, [data]);

  const speedDomain = useMemo(() => {
    if (data.length === 0 || !hasSpeed) return [0, 20];
    const maxVal = Math.max(...data.map((d) => d.speed), ...data.map((d) => d.gap));
    return [0, Math.ceil(maxVal * 1.1)];
  }, [data, hasSpeed]);

  if (loading) {
    return (
      <div className="bg-[#16213e] rounded-2xl border border-slate-700/50 p-4 h-[340px] flex items-center justify-center">
        <div className="flex flex-col items-center gap-3">
          <div className="w-8 h-8 border-2 border-[#00d4ff] border-t-transparent rounded-full animate-spin" />
          <span className="text-sm text-[#a0a0b0]">Computing elevation profile...</span>
        </div>
      </div>
    );
  }

  if (data.length === 0) {
    return (
      <div className="bg-[#16213e] rounded-2xl border border-slate-700/50 p-4 h-[340px] flex items-center justify-center">
        <span className="text-sm text-[#a0a0b0]">No elevation data available</span>
      </div>
    );
  }

  return (
    <div className="bg-[#16213e] rounded-2xl border border-slate-700/50 p-4">
      {/* Header + legend */}
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-2">
          <Mountain size={18} className="text-[#00d4ff]" />
          <h3 className="text-sm font-semibold text-white">Elevation Profile</h3>
        </div>

        {hasSpeed && (
          <div className="flex items-center gap-3">
            <LegendItem
              label="Elevation"
              color={COLORS.elevation}
              active={true}
              dashed={false}
            />
            <LegendItem
              label="Speed"
              color={COLORS.speed}
              active={showSpeed}
              dashed={false}
              onClick={() => setShowSpeed((v) => !v)}
            />
            <LegendItem
              label="GAP"
              color={COLORS.gap}
              active={showGap}
              dashed={true}
              onClick={() => setShowGap((v) => !v)}
            />
          </div>
        )}
      </div>

      <ResponsiveContainer width="100%" height={280}>
        <ComposedChart data={data} margin={{ top: 8, right: 8, bottom: 4, left: 0 }}>
          <defs>
            <linearGradient id="elevGradient" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor={COLORS.elevation} stopOpacity={0.35} />
              <stop offset="100%" stopColor={COLORS.elevation} stopOpacity={0} />
            </linearGradient>
          </defs>

          <CartesianGrid strokeDasharray="3 3" stroke={COLORS.grid} vertical={false} />

          <XAxis
            dataKey="distance"
            tick={{ fill: COLORS.text, fontSize: 11 }}
            axisLine={{ stroke: COLORS.axisLine }}
            tickLine={false}
            tickFormatter={(v: number) => v.toFixed(1)}
            label={{
              value: 'km',
              position: 'insideBottomRight',
              offset: -4,
              fill: COLORS.text,
              fontSize: 10,
            }}
          />

          <YAxis
            yAxisId="elevation"
            orientation="left"
            domain={elevDomain}
            tick={{ fill: COLORS.text, fontSize: 11 }}
            axisLine={false}
            tickLine={false}
            width={50}
            tickFormatter={(v: number) => `${v} m`}
          />

          {hasSpeed && (
            <YAxis
              yAxisId="speed"
              orientation="right"
              domain={speedDomain}
              tick={{ fill: COLORS.text, fontSize: 11 }}
              axisLine={false}
              tickLine={false}
              width={55}
              tickFormatter={(v: number) => `${v}`}
              label={{
                value: 'km/h',
                position: 'insideTopRight',
                offset: -10,
                fill: COLORS.text,
                fontSize: 10,
              }}
            />
          )}

          <Tooltip
            contentStyle={{
              backgroundColor: COLORS.tooltipBg,
              border: '1px solid rgba(255,255,255,0.1)',
              borderRadius: '8px',
              color: '#e0e0e0',
              fontSize: '12px',
            }}
            cursor={{ stroke: 'rgba(255,255,255,0.15)' }}
            labelFormatter={(v) => `${Number(v).toFixed(2)} km`}
            formatter={(value, name) => {
              const labels: Record<string, [string, string]> = {
                elevation: [`${value} m`, 'Elevation'],
                speed: [`${value} km/h`, 'Speed'],
                gap: [`${value} km/h`, 'GAP'],
                grade: [`${value}%`, 'Grade'],
              };
              return labels[name as string] ?? [`${value}`, name];
            }}
          />

          <Area
            yAxisId="elevation"
            type="monotone"
            dataKey="elevation"
            stroke={COLORS.elevation}
            strokeWidth={1.5}
            fill="url(#elevGradient)"
            dot={false}
            activeDot={false}
            name="elevation"
          />

          {hasSpeed && showSpeed && (
            <Line
              yAxisId="speed"
              type="monotone"
              dataKey="speed"
              stroke={COLORS.speed}
              strokeWidth={1.5}
              dot={false}
              activeDot={false}
              name="speed"
            />
          )}

          {hasSpeed && showGap && (
            <Line
              yAxisId="speed"
              type="monotone"
              dataKey="gap"
              stroke={COLORS.gap}
              strokeWidth={1.5}
              strokeDasharray="4 2"
              dot={false}
              activeDot={false}
              name="gap"
            />
          )}
        </ComposedChart>
      </ResponsiveContainer>
    </div>
  );
}

function LegendItem({
  label,
  color,
  active,
  dashed,
  onClick,
}: {
  label: string;
  color: string;
  active: boolean;
  dashed: boolean;
  onClick?: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="flex items-center gap-1.5 text-xs transition-opacity"
      style={{ opacity: active ? 1 : 0.35, cursor: onClick ? 'pointer' : 'default' }}
    >
      <span
        className="inline-block w-4 h-0.5"
        style={{
          backgroundColor: color,
          borderTop: dashed ? `2px dashed ${color}` : undefined,
          height: dashed ? 0 : undefined,
        }}
      />
      <span style={{ color }}>{label}</span>
    </button>
  );
}
