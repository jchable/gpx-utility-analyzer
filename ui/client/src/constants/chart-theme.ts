/**
 * Shared Recharts theme configuration.
 * Used by ElevationProfileChart, ElevationChart, and EditorElevationProfile.
 * Theme-dependent colors use CSS variables — they adapt automatically to light/dark mode.
 */

/** Data series colors for charts */
export const CHART_COLORS = {
  elevation: 'var(--accent)',
  speed: 'var(--accent-orange)',
  gap: 'var(--accent-green)',
  hr: '#ef4444',
  power: '#eab308',
  tobler: '#9333ea',
  hoverDot: 'var(--accent-red)',
  /** Muted text for axis labels */
  text: 'var(--content-muted)',
  /** Grid lines */
  grid: 'var(--chart-grid)',
  /** Axis line */
  axisLine: 'var(--chart-axis)',
  /** Tooltip background */
  tooltipBg: 'var(--surface)',
} as const;

/** Shared Recharts Tooltip contentStyle */
export const TOOLTIP_STYLE: React.CSSProperties = {
  backgroundColor: 'var(--surface)',
  border: '1px solid var(--border-color)',
  borderRadius: '8px',
  color: 'var(--content)',
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
  stroke: 'var(--chart-cursor)',
} as const;
