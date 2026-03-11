import type { TFunction } from 'i18next';

/**
 * Format a duration in seconds using i18n keys for unit labels.
 * Uses `common:duration.d/h/m/s` translation keys.
 * Examples:
 *   EN: "1d 2h 34m 5s"
 *   FR: "1j 2h 34min 5s"
 */
export function formatDuration(totalSeconds: number, t: TFunction): string {
  if (totalSeconds <= 0) return `0${t('duration.s')}`;

  const d = Math.floor(totalSeconds / 86400);
  const h = Math.floor((totalSeconds % 86400) / 3600);
  const m = Math.floor((totalSeconds % 3600) / 60);
  const s = Math.round(totalSeconds % 60);

  const parts: string[] = [];
  if (d > 0) parts.push(`${d}${t('duration.d')}`);
  if (h > 0) parts.push(`${h}${t('duration.h')}`);
  if (m > 0) parts.push(`${m}${t('duration.m')}`);
  if (s > 0 || parts.length === 0) parts.push(`${s}${t('duration.s')}`);

  return parts.join(' ');
}

/**
 * Format a duration for page-level display (hours + minutes only, i18n).
 * Uses `common:format.durationHM` / `common:format.durationM` keys.
 * Example: "2h 34min" / "15min"
 */
export function formatPageDuration(seconds: number, t: TFunction): string {
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  if (h > 0) return t('format.durationHM', { h, m });
  return t('format.durationM', { m });
}

/**
 * Format a short duration for zone display (shows seconds).
 * Not i18n — uses hardcoded compact suffixes.
 * Examples: "30s", "3m 20s", "1h 05m"
 */
export function formatDurationShort(seconds: number): string {
  if (seconds < 60) return `${Math.round(seconds)}s`;
  const m = Math.floor(seconds / 60);
  const s = Math.round(seconds % 60);
  if (m < 60) return s > 0 ? `${m}m ${s}s` : `${m}m`;
  const h = Math.floor(m / 60);
  const rm = m % 60;
  return rm > 0 ? `${h}h ${rm}m` : `${h}h`;
}

/**
 * Format a compact duration for the editor toolbar.
 * Examples: "1h05", "15min", "--"
 */
export function formatDurationCompact(seconds: number): string {
  if (seconds <= 0) return '--';
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  if (h > 0) return `${h}h${m.toString().padStart(2, '0')}`;
  return `${m}min`;
}

/**
 * Format a date string with locale-aware options.
 */
export function formatDate(
  iso: string,
  lang: string,
  options?: Intl.DateTimeFormatOptions,
): string {
  return new Date(iso).toLocaleDateString(lang, options ?? {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  });
}
