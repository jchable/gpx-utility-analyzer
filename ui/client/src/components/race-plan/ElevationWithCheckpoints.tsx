import { useMemo, useCallback } from 'react';
import {
  AreaChart,
  Area,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  ReferenceLine,
  ReferenceArea,
} from 'recharts';
import { useTranslation } from 'react-i18next';
import type { RacePlanDetail } from '../../types/race-plan';
import type { DayNightSegment } from '../../utils/dayNight';
import { formatArrivalTime } from '../../utils/dayNight';
import { useRacePlanStore } from '../../stores/racePlanStore';
import {
  CHART_COLORS,
  TOOLTIP_STYLE,
  AXIS_TICK,
  AXIS_LINE,
  GRID_PROPS,
} from '../../constants/chart-theme';

const CHECKPOINT_COLORS: Record<string, string> = {
  start: '#22c55e',
  finish: '#ef4444',
  aid_station: '#06b6d4',
  checkpoint: '#f59e0b',
  crew_only: '#a855f7',
};

interface Props {
  plan: RacePlanDetail;
  dayNightSegments?: DayNightSegment[];
  readOnly?: boolean;
}

export default function ElevationWithCheckpoints({ plan, dayNightSegments, readOnly }: Props) {
  const { t } = useTranslation('race-plans');
  const { setHoveredDistanceKm, openCheckpointEditor } = useRacePlanStore();
  const startTime = plan.startTime ?? '00:00';

  const data = useMemo(() => plan.profile ?? [], [plan.profile]);

  const elevDomain = useMemo(() => {
    if (data.length === 0) return [0, 100];
    const eles = data.map((d) => d.elevation);
    const min = Math.min(...eles);
    const max = Math.max(...eles);
    const padding = Math.max((max - min) * 0.08, 20);
    return [Math.floor(min - padding), Math.ceil(max + padding)];
  }, [data]);

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const handleMouseMove = useCallback((state: any) => {
    if (state?.activePayload?.[0]) {
      setHoveredDistanceKm(state.activePayload[0].payload.distance as number);
    }
  }, [setHoveredDistanceKm]);

  const handleMouseLeave = useCallback(() => {
    setHoveredDistanceKm(null);
  }, [setHoveredDistanceKm]);

  // Click on chart → add checkpoint at that distance
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const handleClick = useCallback((state: any) => {
    if (readOnly || !state?.activePayload?.[0]) return;
    openCheckpointEditor(null, state.activePayload[0].payload.distance as number);
  }, [readOnly, openCheckpointEditor]);

  if (data.length === 0) {
    return (
      <div className="h-40 flex items-center justify-center text-content-muted text-sm">
        {t('noPlans', { defaultValue: 'No profile data' })}
      </div>
    );
  }

  const sortedCheckpoints = [...plan.checkpoints].sort((a, b) => a.order - b.order);

  return (
    <div className="w-full">
      <ResponsiveContainer width="100%" height={160}>
        <AreaChart
          data={data}
          margin={{ top: 8, right: 8, bottom: 0, left: 0 }}
          onMouseMove={handleMouseMove}
          onMouseLeave={handleMouseLeave}
          onClick={!readOnly ? handleClick : undefined}
          style={{ cursor: readOnly ? 'default' : 'crosshair' }}
        >
          {/* Day/Night background */}
          {dayNightSegments?.map((seg, i) =>
            seg.isNight ? (
              <ReferenceArea
                key={i}
                x1={seg.fromDistanceKm}
                x2={seg.toDistanceKm}
                fill="#1e2d4d"
                fillOpacity={0.6}
              />
            ) : null,
          )}

          <CartesianGrid {...GRID_PROPS} />
          <XAxis
            dataKey="distance"
            tick={AXIS_TICK}
            axisLine={AXIS_LINE}
            tickLine={false}
            tickFormatter={(v: number) => `${v.toFixed(0)}km`}
          />
          <YAxis
            domain={elevDomain}
            tick={AXIS_TICK}
            axisLine={AXIS_LINE}
            tickLine={false}
            width={40}
            tickFormatter={(v: number) => `${Math.round(v)}m`}
          />
          <Tooltip
            contentStyle={TOOLTIP_STYLE}
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            formatter={(value: any) => [`${Math.round(Number(value))} m`, 'Elev.']}
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            labelFormatter={(label: any) => `${Number(label).toFixed(1)} km`}
          />

          <Area
            type="monotone"
            dataKey="elevation"
            stroke={CHART_COLORS.elevation}
            strokeWidth={2}
            fill={CHART_COLORS.elevation}
            fillOpacity={0.15}
            dot={false}
            isAnimationActive={false}
          />

          {/* Checkpoint reference lines */}
          {sortedCheckpoints.map((cp) => {
            const color = CHECKPOINT_COLORS[cp.type] ?? '#94a3b8';
            const arrival = cp.targetArrivalSeconds != null
              ? formatArrivalTime(startTime, cp.targetArrivalSeconds)
              : null;
            return (
              <ReferenceLine
                key={cp.id}
                x={cp.distanceKm}
                stroke={color}
                strokeWidth={1.5}
                strokeDasharray="4 2"
                label={{
                  value: arrival ? `${cp.name}\n${arrival}` : cp.name,
                  position: 'insideTopRight',
                  fill: color,
                  fontSize: 9,
                }}
              />
            );
          })}
        </AreaChart>
      </ResponsiveContainer>

      {/* Legend */}
      {dayNightSegments && dayNightSegments.some((s) => s.isNight) && (
        <div className="flex items-center gap-4 mt-2 px-2">
          <div className="flex items-center gap-1.5 text-xs text-content-muted">
            <div className="w-3 h-3 rounded-sm bg-white/10 border border-white/20" />
            <span>{t('dayNight.day')}</span>
          </div>
          <div className="flex items-center gap-1.5 text-xs text-content-muted">
            <div className="w-3 h-3 rounded-sm bg-blue-900/60 border border-blue-800/40" />
            <span>{t('dayNight.night')}</span>
          </div>
          {!readOnly && (
            <span className="ml-auto text-xs text-content-muted/60 italic">
              Click to add checkpoint
            </span>
          )}
        </div>
      )}
    </div>
  );
}
