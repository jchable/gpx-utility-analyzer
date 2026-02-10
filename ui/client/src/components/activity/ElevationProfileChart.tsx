import { useState, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
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
import { Mountain, Clock } from 'lucide-react';
import type { ProfilePoint } from '../../types/activity';
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
  const { t } = useTranslation('activities');
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

  const totalDuration = useMemo(() => {
    if (data.length === 0) return 0;
    const last = data[data.length - 1].elapsedTime;
    return last ?? 0;
  }, [data]);

  if (loading) {
    return (
      <div className="bg-[#16213e] rounded-2xl border border-slate-700/50 p-4 h-[340px] flex items-center justify-center">
        <div className="flex flex-col items-center gap-3">
          <div className="w-8 h-8 border-2 border-[#00d4ff] border-t-transparent rounded-full animate-spin" />
          <span className="text-sm text-[#a0a0b0]">{t('elevation.computing')}</span>
        </div>
      </div>
    );
  }

  if (data.length === 0) {
    return (
      <div className="bg-[#16213e] rounded-2xl border border-slate-700/50 p-4 h-[340px] flex items-center justify-center">
        <span className="text-sm text-[#a0a0b0]">{t('elevation.noData')}</span>
      </div>
    );
  }

  return (
    <div className="bg-[#16213e] rounded-2xl border border-slate-700/50 p-4">
      {/* Header + legend */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2 mb-4">
        <div className="flex items-center gap-2">
          <Mountain size={18} className="text-[#00d4ff]" />
          <h3 className="text-sm font-semibold text-white">{t('elevation.profile')}</h3>
        </div>

        <div className="flex items-center gap-3 flex-wrap">
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
                {t('elevation.time')}
              </button>
            </div>
          )}

          {hasSpeed && (
            <>
              <LegendItem label={t('elevation.elevation')} color={COLORS.elevation} active={true} dashed={false} />
              <LegendItem
                label={t('elevation.speed')}
                color={COLORS.speed}
                active={showSpeed}
                dashed={false}
                onClick={() => setShowSpeed((v) => !v)}
              />
              <LegendItem
                label={t('elevation.gap')}
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
                elevation: [`${value} m`, t('elevation.elevation')],
                speed: [`${value} km/h`, t('elevation.speed')],
                gap: [`${value} km/h`, t('elevation.gap')],
                grade: [`${value}%`, t('elevation.grade')],
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
            isAnimationActive={false}
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
              isAnimationActive={false}
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
              isAnimationActive={false}
              name="gap"
            />
          )}
        </ComposedChart>
      </ResponsiveContainer>

      {/* Stop timeline band (time mode only) */}
      {xMode === 'time' && stopAreas.length > 0 && totalDuration > 0 && (
        <div className="flex items-center gap-2 mt-1">
          <span className="text-[10px] text-slate-500 w-[50px] text-right">{t('elevation.stops')}</span>
          <div
            className="relative h-2.5 flex-1 bg-slate-800/40 rounded-sm overflow-hidden"
            style={{ marginRight: hasSpeed ? 63 : 8 }}
          >
            {stopAreas.map((stop, i) => (
              <div
                key={i}
                className="absolute top-0 h-full bg-red-400/40 border-x border-red-400/60 rounded-sm"
                style={{
                  left: `${(stop.x1 / totalDuration) * 100}%`,
                  width: `${Math.max(((stop.x2 - stop.x1) / totalDuration) * 100, 0.3)}%`,
                }}
                title={stop.label}
              />
            ))}
          </div>
        </div>
      )}
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
