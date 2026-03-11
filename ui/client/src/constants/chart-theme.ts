/**
 * Shared Recharts theme configuration.
 * Used by ElevationProfileChart, ElevationChart, and EditorElevationProfile.
 */

/** Data series colors for charts */
export const CHART_COLORS = {
  elevation: '#00d4ff',
  speed: '#ff8800',
  gap: '#00ff88',
  hr: '#ef4444',
  power: '#eab308',
  tobler: '#9333ea',
  hoverDot: '#ff6b6b',
  /** Muted text for axis labels */
  text: '#a0a0b0',
  /** Grid lines */
  grid: 'rgba(255,255,255,0.05)',
  /** Axis line */
  axisLine: 'rgba(255,255,255,0.08)',
  /** Tooltip background */
  tooltipBg: '#0f0f1a',
} as const;

/** Shared Recharts Tooltip contentStyle */
export const TOOLTIP_STYLE: React.CSSProperties = {
  backgroundColor: CHART_COLORS.tooltipBg,
  border: '1px solid rgba(255,255,255,0.1)',
  borderRadius: '8px',
  color: '#e0e0e0',
  fontSize: '12px',
};

/** Compact variant for editor charts */
export const TOOLTIP_STYLE_COMPACT: React.CSSProperties = {
  ...TOOLTIP_STYLE,
  fontSize: '11px',
  padding: '6px 10px',
};

/** Shared axis tick style */
export const AXIS_TICK = {
  fill: CHART_COLORS.text,
  fontSize: 11,
} as const;

/** Compact axis tick style for editor charts */
export const AXIS_TICK_COMPACT = {
  fill: CHART_COLORS.text,
  fontSize: 10,
} as const;

/** Shared axis line style */
export const AXIS_LINE = {
  stroke: CHART_COLORS.axisLine,
} as const;

/** Shared CartesianGrid props */
export const GRID_PROPS = {
  strokeDasharray: '3 3',
  stroke: CHART_COLORS.grid,
  vertical: false,
} as const;

/** Shared Tooltip cursor style */
export const TOOLTIP_CURSOR = {
  stroke: 'rgba(255,255,255,0.15)',
} as const;
