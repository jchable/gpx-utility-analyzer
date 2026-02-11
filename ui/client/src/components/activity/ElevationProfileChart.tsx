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
import { formatDuration } from '../../utils/format';

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
  hr: '#ef4444',
  power: '#eab308',
  text: '#a0a0b0',
  grid: 'rgba(255,255,255,0.05)',
  axisLine: 'rgba(255,255,255,0.08)',
  tooltipBg: '#0f0f1a',
} as const;

type OverlayMode = 'speed' | 'hr' | 'power';

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
  const { t: tc } = useTranslation();
  const [showSpeed, setShowSpeed] = useState(true);
  const [showGap, setShowGap] = useState(true);
  const [xMode, setXMode] = useState<'distance' | 'time'>('distance');
  const [overlayMode, setOverlayMode] = useState<OverlayMode>('speed');

  const xDataKey = xMode === 'time' ? 'elapsedTime' : 'distance';

  const hasSpeed = useMemo(() => data.some((d) => d.speed > 0), [data]);
  const hasHR = useMemo(() => data.some((d) => d.heartRate != null && d.heartRate > 0), [data]);
  const hasPower = useMemo(() => data.some((d) => d.power != null && d.power > 0), [data]);

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

  const hrDomain = useMemo(() => {
    if (data.length === 0 || !hasHR) return [60, 200];
    const hrs = data.filter((d) => d.heartRate != null).map((d) => d.heartRate!);
    if (hrs.length === 0) return [60, 200];
    const min = Math.min(...hrs);
    const max = Math.max(...hrs);
    return [Math.max(Math.floor(min - 10), 40), Math.ceil(max + 10)];
  }, [data, hasHR]);

  const powerDomain = useMemo(() => {
    if (data.length === 0 || !hasPower) return [0, 400];
    const pws = data.filter((d) => d.power != null).map((d) => d.power!);
    if (pws.length === 0) return [0, 400];
    return [0, Math.ceil(Math.max(...pws) * 1.1)];
  }, [data, hasPower]);

  const rightDomain = overlayMode === 'hr' ? hrDomain : overlayMode === 'power' ? powerDomain : speedDomain;
  const rightLabel = overlayMode === 'hr' ? 'bpm' : overlayMode === 'power' ? 'W' : 'km/h';

  // Stop band uses data point indices to match categorical chart positioning
  const stopAreas = useMemo(() => {
    if (!stops || stops.length === 0 || !activityStartTime || data.length < 2) return [];
    const startMs = new Date(activityStartTime).getTime();
    const n = data.length;

    const timeToIndex = (elapsed: number): number => {
      for (let i = 0; i < n; i++) {
        if ((data[i].elapsedTime ?? 0) >= elapsed) return i;
      }
      return n - 1;
    };

    return stops.map((stop) => ({
      idx1: timeToIndex((new Date(stop.start_time).getTime() - startMs) / 1000),
      idx2: timeToIndex((new Date(stop.end_time).getTime() - startMs) / 1000),
      label: formatDuration(stop.duration.seconds, tc),
    }));
  }, [stops, activityStartTime, tc, data]);

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

          <LegendItem label={t('elevation.elevation')} color={COLORS.elevation} active={true} dashed={false} />

          {/* Overlay mode selector */}
          {(hasSpeed || hasHR || hasPower) && (
            <div className="flex items-center bg-slate-800/60 rounded-lg p-0.5">
              {hasSpeed && (
                <button
                  type="button"
                  onClick={() => setOverlayMode('speed')}
                  className={`px-2 py-1 rounded-md text-xs transition-colors ${
                    overlayMode === 'speed' ? 'bg-slate-700 text-white' : 'text-slate-400 hover:text-slate-300'
                  }`}
                >
                  {t('elevation.overlaySpeed')}
                </button>
              )}
              {hasHR && (
                <button
                  type="button"
                  onClick={() => setOverlayMode('hr')}
                  className={`px-2 py-1 rounded-md text-xs transition-colors ${
                    overlayMode === 'hr' ? 'bg-slate-700 text-white' : 'text-slate-400 hover:text-slate-300'
                  }`}
                >
                  {t('elevation.overlayHR')}
                </button>
              )}
              {hasPower && (
                <button
                  type="button"
                  onClick={() => setOverlayMode('power')}
                  className={`px-2 py-1 rounded-md text-xs transition-colors ${
                    overlayMode === 'power' ? 'bg-slate-700 text-white' : 'text-slate-400 hover:text-slate-300'
                  }`}
                >
                  {t('elevation.overlayPower')}
                </button>
              )}
            </div>
          )}

          {/* Speed/GAP toggles (only when speed mode active) */}
          {overlayMode === 'speed' && hasSpeed && (
            <>
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
          {overlayMode === 'hr' && hasHR && (
            <LegendItem label={t('elevation.overlayHR')} color={COLORS.hr} active={true} dashed={false} />
          )}
          {overlayMode === 'power' && hasPower && (
            <LegendItem label={t('elevation.overlayPower')} color={COLORS.power} active={true} dashed={false} />
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

          {(hasSpeed || hasHR || hasPower) && (
            <YAxis
              yAxisId="right"
              orientation="right"
              domain={rightDomain}
              tick={{ fill: COLORS.text, fontSize: 11 }}
              axisLine={false}
              tickLine={false}
              width={55}
              tickFormatter={(v: number) => `${v}`}
              label={{
                value: rightLabel,
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
                heartRate: [`${value} bpm`, t('elevation.overlayHR')],
                power: [`${value} W`, t('elevation.overlayPower')],
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

          {overlayMode === 'speed' && hasSpeed && showSpeed && (
            <Line
              yAxisId="right"
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

          {overlayMode === 'speed' && hasSpeed && showGap && (
            <Line
              yAxisId="right"
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

          {overlayMode === 'hr' && hasHR && (
            <Line
              yAxisId="right"
              type="monotone"
              dataKey="heartRate"
              stroke={COLORS.hr}
              strokeWidth={1.5}
              dot={false}
              activeDot={false}
              isAnimationActive={false}
              name="heartRate"
              connectNulls
            />
          )}

          {overlayMode === 'power' && hasPower && (
            <Line
              yAxisId="right"
              type="monotone"
              dataKey="power"
              stroke={COLORS.power}
              strokeWidth={1.5}
              dot={false}
              activeDot={false}
              isAnimationActive={false}
              name="power"
              connectNulls
            />
          )}
        </ComposedChart>
      </ResponsiveContainer>

      {/* Stop timeline band — index-based to match categorical chart */}
      {stopAreas.length > 0 && data.length > 1 && (
        <div className="flex items-center mt-1" style={{ paddingLeft: 50, paddingRight: (hasSpeed || hasHR || hasPower) ? 63 : 8 }}>
          <span className="text-[10px] text-slate-500 shrink-0 -ml-[50px] w-[50px] text-right pr-2">{t('elevation.stops')}</span>
          <div className="relative h-2.5 w-full bg-slate-800/40 rounded-sm overflow-hidden">
            {stopAreas.map((stop, i) => {
              const total = data.length - 1;
              return (
                <div
                  key={i}
                  className="absolute top-0 h-full bg-red-400/40 border-x border-red-400/60 rounded-sm"
                  style={{
                    left: `${(stop.idx1 / total) * 100}%`,
                    width: `${Math.max(((stop.idx2 - stop.idx1) / total) * 100, 0.3)}%`,
                  }}
                  title={stop.label}
                />
              );
            })}
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
