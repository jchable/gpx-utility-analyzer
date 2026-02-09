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
  ReferenceArea,
} from 'recharts';
import { Mountain, Clock } from 'lucide-react';
import type { ProfilePoint } from '../../utils/gpx';
import type { StopInfo } from '../../types/activity';

interface ElevationProfileChartProps {
  data: ProfilePoint[];
  loading?: boolean;
  stops?: StopInfo[];
  hasTimestamps?: boolean;
  activityStartTime?: string;
}

const COLORS = {
  elevation: '#00d4ff',
  speed: '#ff8800',
  gap: '#00ff88',
  text: '#a0a0b0',
  grid: 'rgba(255,255,255,0.05)',
  axisLine: 'rgba(255,255,255,0.08)',
  tooltipBg: '#0f0f1a',
  stop: 'rgba(255, 100, 100, 0.12)',
  stopStroke: 'rgba(255, 100, 100, 0.25)',
} as const;

/** Format elapsed seconds as h:mm:ss or m:ss */
function formatElapsedTime(seconds: number): string {
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  const s = Math.floor(seconds % 60);
  if (h > 0) {
    return `${h}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
  }
  return `${m}:${String(s).padStart(2, '0')}`;
}

export default function ElevationProfileChart({
  data,
  loading,
  stops,
  hasTimestamps = false,
  activityStartTime,
}: ElevationProfileChartProps) {
  const [showSpeed, setShowSpeed] = useState(true);
  const [showGap, setShowGap] = useState(true);
  const [xMode, setXMode] = useState<'distance' | 'time'>('distance');

  const xDataKey = xMode === 'time' ? 'elapsedTime' : 'distance';

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

  const stopAreas = useMemo(() => {
    if (xMode !== 'time' || !stops || stops.length === 0 || !activityStartTime) {
      return [];
    }
    const startMs = new Date(activityStartTime).getTime();
    return stops.map((stop) => ({
      x1: (new Date(stop.start_time).getTime() - startMs) / 1000,
      x2: (new Date(stop.end_time).getTime() - startMs) / 1000,
      label: stop.duration.display,
    }));
  }, [xMode, stops, activityStartTime]);

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

        <div className="flex items-center gap-3">
          {/* Distance / Time toggle */}
          {hasTimestamps && (
            <div className="flex items-center bg-slate-800/60 rounded-lg p-0.5">
              <button
                type="button"
                onClick={() => setXMode('distance')}
                className={`flex items-center gap-1 px-2 py-1 rounded-md text-xs transition-colors ${
                  xMode === 'distance'
                    ? 'bg-slate-700 text-white'
                    : 'text-slate-400 hover:text-slate-300'
                }`}
              >
                <Mountain size={12} />
                km
              </button>
              <button
                type="button"
                onClick={() => setXMode('time')}
                className={`flex items-center gap-1 px-2 py-1 rounded-md text-xs transition-colors ${
                  xMode === 'time'
                    ? 'bg-slate-700 text-white'
                    : 'text-slate-400 hover:text-slate-300'
                }`}
              >
                <Clock size={12} />
                time
              </button>
            </div>
          )}

          {hasSpeed && (
            <>
              <LegendItem label="Elevation" color={COLORS.elevation} active={true} dashed={false} />
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
            </>
          )}
        </div>
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
            dataKey={xDataKey}
            tick={{ fill: COLORS.text, fontSize: 11 }}
            axisLine={{ stroke: COLORS.axisLine }}
            tickLine={false}
            tickFormatter={(v: number) =>
              xMode === 'time' ? formatElapsedTime(v) : v.toFixed(1)
            }
            label={
              xMode === 'distance'
                ? {
                    value: 'km',
                    position: 'insideBottomRight',
                    offset: -4,
                    fill: COLORS.text,
                    fontSize: 10,
                  }
                : undefined
            }
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
            labelFormatter={(v) =>
              xMode === 'time'
                ? formatElapsedTime(Number(v))
                : `${Number(v).toFixed(2)} km`
            }
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

          {/* Stop zones (time mode only) */}
          {stopAreas.map((stop, i) => (
            <ReferenceArea
              key={`stop-${i}`}
              x1={stop.x1}
              x2={stop.x2}
              yAxisId="elevation"
              fill={COLORS.stop}
              stroke={COLORS.stopStroke}
              strokeWidth={0.5}
              ifOverflow="hidden"
              label={
                stopAreas.length <= 10
                  ? {
                      value: stop.label,
                      position: 'insideTop',
                      fill: 'rgba(255,150,150,0.6)',
                      fontSize: 9,
                    }
                  : undefined
              }
            />
          ))}
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
