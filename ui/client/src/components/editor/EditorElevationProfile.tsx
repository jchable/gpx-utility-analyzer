import { useMemo, useCallback, useState, useRef, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import {
  ComposedChart,
  Area,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  ReferenceDot,
  ReferenceArea,
} from 'recharts';
import { Mountain, ChevronDown, ChevronUp, Scissors, Check, X } from 'lucide-react';
import { useEditorStore } from '../../stores/editorStore';
import { useRouteStats } from '../../hooks/useRouteStats';
import {
  CHART_COLORS,
  TOOLTIP_STYLE_COMPACT,
  AXIS_TICK_COMPACT,
  AXIS_LINE,
  GRID_PROPS,
  TOOLTIP_CURSOR,
} from '../../constants/chart-theme';

interface ProfileData {
  index: number;
  distance: number;
  elevation: number;
}

interface EditorElevationProfileProps {
  collapsed?: boolean;
  onToggle?: () => void;
}

export default function EditorElevationProfile({
  collapsed = false,
  onToggle,
}: EditorElevationProfileProps) {
  const { t } = useTranslation('routes');

  const routeCoordinates = useEditorStore((s) => s.routeCoordinates);
  const hoveredPointIndex = useEditorStore((s) => s.hoveredPointIndex);
  const setHoveredPointIndex = useEditorStore((s) => s.setHoveredPointIndex);
  const mode = useEditorStore((s) => s.mode);
  const cropRoute = useEditorStore((s) => s.cropRoute);

  const stats = useRouteStats(routeCoordinates);

  // Crop state — single array state to avoid multiple setState calls
  const [cropRange, setCropRange] = useState<[number, number]>([0, 0]);
  const isCropMode = mode === 'crop';
  const cropStart = cropRange[0];
  const cropEnd = cropRange[1];

  // Reset crop range when entering crop mode
  useEffect(() => {
    if (isCropMode && routeCoordinates.length > 0) {
      setCropRange([0, routeCoordinates.length - 1]);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isCropMode]);

  const setCropStart = useCallback((v: number) => setCropRange((r) => [v, r[1]]), []);
  const setCropEnd = useCallback((v: number) => setCropRange((r) => [r[0], v]), []);

  // Build profile data from coordinates
  const profileData = useMemo((): ProfileData[] => {
    if (routeCoordinates.length < 2) return [];

    const data: ProfileData[] = [];
    let cumulativeDist = 0;

    for (let i = 0; i < routeCoordinates.length; i++) {
      if (i > 0) {
        const prev = routeCoordinates[i - 1];
        const curr = routeCoordinates[i];

        // Haversine distance
        const dLat = (curr[1] - prev[1]) * (Math.PI / 180);
        const dLon = (curr[0] - prev[0]) * (Math.PI / 180);
        const a =
          Math.sin(dLat / 2) ** 2 +
          Math.cos(prev[1] * (Math.PI / 180)) *
            Math.cos(curr[1] * (Math.PI / 180)) *
            Math.sin(dLon / 2) ** 2;
        cumulativeDist += 6371 * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
      }

      data.push({
        index: i,
        distance: Math.round(cumulativeDist * 1000) / 1000,
        elevation: routeCoordinates[i][2] ?? 0,
      });
    }

    // Downsample if too many points (keep chart responsive)
    if (data.length > 1000) {
      const step = Math.ceil(data.length / 1000);
      const downsampled: ProfileData[] = [];
      for (let i = 0; i < data.length; i += step) {
        downsampled.push(data[i]);
      }
      if (downsampled[downsampled.length - 1] !== data[data.length - 1]) {
        downsampled.push(data[data.length - 1]);
      }
      return downsampled;
    }

    return data;
  }, [routeCoordinates]);

  const elevDomain = useMemo(() => {
    if (profileData.length === 0) return [0, 100];
    const eles = profileData.map((d) => d.elevation);
    const min = Math.min(...eles);
    const max = Math.max(...eles);
    const padding = Math.max((max - min) * 0.1, 20);
    return [Math.floor(min - padding), Math.ceil(max + padding)];
  }, [profileData]);

  // Find the hovered data point for ReferenceDot
  const hoveredData = useMemo(() => {
    if (hoveredPointIndex === null || profileData.length === 0) return null;
    return profileData.find((d) => d.index === hoveredPointIndex) ??
      profileData.reduce((closest, d) =>
        Math.abs(d.index - hoveredPointIndex) < Math.abs(closest.index - hoveredPointIndex) ? d : closest
      );
  }, [hoveredPointIndex, profileData]);

  // Crop range distances (for ReferenceArea)
  const cropStartDist = useMemo(() => {
    if (!isCropMode || profileData.length === 0) return 0;
    const pt = profileData.find((d) => d.index >= cropStart);
    return pt?.distance ?? 0;
  }, [isCropMode, cropStart, profileData]);

  const cropEndDist = useMemo(() => {
    if (!isCropMode || profileData.length === 0) return 0;
    for (let i = profileData.length - 1; i >= 0; i--) {
      if (profileData[i].index <= cropEnd) return profileData[i].distance;
    }
    return profileData[profileData.length - 1]?.distance ?? 0;
  }, [isCropMode, cropEnd, profileData]);

  // Sync chart → map: on mouse move over chart
  const handleMouseMove = useCallback(
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    (state: any) => {
      if (state?.activePayload?.[0]?.payload) {
        const payload = state.activePayload[0].payload as ProfileData;
        setHoveredPointIndex(payload.index);
      }
    },
    [setHoveredPointIndex],
  );

  const handleMouseLeave = useCallback(() => {
    setHoveredPointIndex(null);
  }, [setHoveredPointIndex]);

  const handleCropApply = useCallback(() => {
    cropRoute(cropStart, cropEnd);
    useEditorStore.getState().setMode('select');
  }, [cropStart, cropEnd, cropRoute]);

  const handleCropCancel = useCallback(() => {
    useEditorStore.getState().setMode('select');
  }, []);

  const hasElevation = routeCoordinates.some((c) => c.length >= 3 && c[2] !== 0);
  const maxIndex = routeCoordinates.length > 0 ? routeCoordinates.length - 1 : 0;

  // Header bar (always visible)
  const header = (
    <button
      onClick={onToggle}
      className="flex items-center justify-between w-full px-4 py-2 bg-surface-card hover:bg-surface-alt transition-colors"
    >
      <div className="flex items-center gap-2">
        <Mountain size={16} className="text-accent" />
        <span className="text-xs font-medium text-content">{t('stats.distance')}: {stats.distanceKm.toFixed(1)} km</span>
        {hasElevation && (
          <>
            <span className="text-xs text-content-muted mx-1">|</span>
            <span className="text-xs text-content-muted">D+ {stats.elevationGain}m</span>
            <span className="text-xs text-content-muted">D- {stats.elevationLoss}m</span>
          </>
        )}
      </div>
      {collapsed ? <ChevronUp size={16} className="text-content-muted" /> : <ChevronDown size={16} className="text-content-muted" />}
    </button>
  );

  if (collapsed || profileData.length === 0) {
    return <div className="border-t border-border">{header}</div>;
  }

  return (
    <div className="border-t border-border bg-surface-card">
      {header}

      {/* Crop controls */}
      {isCropMode && maxIndex > 0 && (
        <CropSlider
          cropStart={cropStart}
          cropEnd={cropEnd}
          maxIndex={maxIndex}
          onStartChange={setCropStart}
          onEndChange={setCropEnd}
          onApply={handleCropApply}
          onCancel={handleCropCancel}
        />
      )}

      <div className="px-2 pb-2">
        <ResponsiveContainer width="100%" height={160}>
          <ComposedChart
            data={profileData}
            margin={{ top: 4, right: 8, bottom: 0, left: 0 }}
            onMouseMove={handleMouseMove}
            onMouseLeave={handleMouseLeave}
          >
            <defs>
              <linearGradient id="editorElevGradient" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stopColor={CHART_COLORS.elevation} stopOpacity={0.35} />
                <stop offset="100%" stopColor={CHART_COLORS.elevation} stopOpacity={0} />
              </linearGradient>
            </defs>

            <CartesianGrid {...GRID_PROPS} />

            <XAxis
              dataKey="distance"
              tick={AXIS_TICK_COMPACT}
              axisLine={AXIS_LINE}
              tickLine={false}
              tickFormatter={(v: number) => `${v.toFixed(1)}`}
              label={{
                value: 'km',
                position: 'insideBottomRight',
                offset: -4,
                fill: CHART_COLORS.text,
                fontSize: 9,
              }}
            />

            <YAxis
              yAxisId="elevation"
              orientation="left"
              domain={elevDomain}
              tick={AXIS_TICK_COMPACT}
              axisLine={false}
              tickLine={false}
              width={45}
              tickFormatter={(v: number) => `${v}m`}
            />

            <Tooltip
              contentStyle={TOOLTIP_STYLE_COMPACT}
              cursor={TOOLTIP_CURSOR}
              labelFormatter={(v) => `${Number(v).toFixed(2)} km`}
              formatter={(value: number | undefined) => [`${Math.round(value ?? 0)} m`, t('stats.elevationGain').replace(' +', '')]}
            />

            {/* Crop overlay: dim the portions outside the crop range */}
            {isCropMode && cropStartDist > 0 && (
              <ReferenceArea
                yAxisId="elevation"
                x1={profileData[0]?.distance}
                x2={cropStartDist}
                fill="rgba(0,0,0,0.5)"
                fillOpacity={1}
              />
            )}
            {isCropMode && cropEndDist < (profileData[profileData.length - 1]?.distance ?? 0) && (
              <ReferenceArea
                yAxisId="elevation"
                x1={cropEndDist}
                x2={profileData[profileData.length - 1]?.distance}
                fill="rgba(0,0,0,0.5)"
                fillOpacity={1}
              />
            )}

            <Area
              yAxisId="elevation"
              type="monotone"
              dataKey="elevation"
              stroke={CHART_COLORS.elevation}
              strokeWidth={1.5}
              fill="url(#editorElevGradient)"
              dot={false}
              activeDot={false}
              isAnimationActive={false}
            />

            {/* Hover dot synced with map */}
            {hoveredData && (
              <ReferenceDot
                yAxisId="elevation"
                x={hoveredData.distance}
                y={hoveredData.elevation}
                r={5}
                fill={CHART_COLORS.hoverDot}
                stroke="#ffffff"
                strokeWidth={2}
              />
            )}
          </ComposedChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
}

// --- Crop Range Slider Component ---

interface CropSliderProps {
  cropStart: number;
  cropEnd: number;
  maxIndex: number;
  onStartChange: (v: number) => void;
  onEndChange: (v: number) => void;
  onApply: () => void;
  onCancel: () => void;
}

function CropSlider({ cropStart, cropEnd, maxIndex, onStartChange, onEndChange, onApply, onCancel }: CropSliderProps) {
  const { t } = useTranslation('routes');
  const trackRef = useRef<HTMLDivElement>(null);
  const [dragging, setDragging] = useState<'start' | 'end' | null>(null);

  const startPct = (cropStart / maxIndex) * 100;
  const endPct = (cropEnd / maxIndex) * 100;

  const handlePointerDown = useCallback((handle: 'start' | 'end') => {
    setDragging(handle);
  }, []);

  useEffect(() => {
    if (!dragging) return;

    const handlePointerMove = (e: PointerEvent) => {
      const track = trackRef.current;
      if (!track) return;

      const rect = track.getBoundingClientRect();
      const pct = Math.max(0, Math.min(1, (e.clientX - rect.left) / rect.width));
      const idx = Math.round(pct * maxIndex);

      if (dragging === 'start') {
        onStartChange(Math.min(idx, cropEnd - 1));
      } else {
        onEndChange(Math.max(idx, cropStart + 1));
      }
    };

    const handlePointerUp = () => setDragging(null);

    window.addEventListener('pointermove', handlePointerMove);
    window.addEventListener('pointerup', handlePointerUp);
    return () => {
      window.removeEventListener('pointermove', handlePointerMove);
      window.removeEventListener('pointerup', handlePointerUp);
    };
  }, [dragging, maxIndex, cropStart, cropEnd, onStartChange, onEndChange]);

  return (
    <div className="px-4 py-2 border-b border-border">
      <div className="flex items-center gap-2 mb-2">
        <Scissors size={12} className="text-amber-400" />
        <span className="text-[10px] text-amber-400 font-medium uppercase tracking-wider">
          {t('editor.toolbar.crop')}
        </span>
        <span className="text-[10px] text-content-muted ml-1">
          {t('editor.cropHint')}
        </span>
        <div className="flex-1" />
        <button
          onClick={onCancel}
          className="flex items-center gap-1 text-[10px] text-content-muted hover:text-content transition-colors"
        >
          <X size={12} />
          {t('editor.cropCancel')}
        </button>
        <button
          onClick={onApply}
          className="flex items-center gap-1 text-[10px] text-accent hover:text-content transition-colors"
        >
          <Check size={12} />
          {t('editor.cropApply')}
        </button>
      </div>

      {/* Range slider track */}
      <div
        ref={trackRef}
        className="relative h-4 select-none touch-none"
      >
        {/* Background track */}
        <div className="absolute top-1/2 -translate-y-1/2 left-0 right-0 h-1 bg-surface-alt/50 rounded-full" />

        {/* Selected range */}
        <div
          className="absolute top-1/2 -translate-y-1/2 h-1 bg-accent rounded-full"
          style={{ left: `${startPct}%`, width: `${endPct - startPct}%` }}
        />

        {/* Start handle */}
        <div
          className="absolute top-1/2 -translate-y-1/2 -translate-x-1/2 w-3 h-3 bg-white rounded-full border-2 border-accent cursor-ew-resize shadow-lg"
          style={{ left: `${startPct}%` }}
          onPointerDown={() => handlePointerDown('start')}
        />

        {/* End handle */}
        <div
          className="absolute top-1/2 -translate-y-1/2 -translate-x-1/2 w-3 h-3 bg-white rounded-full border-2 border-accent cursor-ew-resize shadow-lg"
          style={{ left: `${endPct}%` }}
          onPointerDown={() => handlePointerDown('end')}
        />
      </div>

      <div className="flex justify-between text-[9px] text-content-muted mt-1">
        <span>pt {cropStart}</span>
        <span>{cropEnd - cropStart + 1} pts</span>
        <span>pt {cropEnd}</span>
      </div>
    </div>
  );
}
