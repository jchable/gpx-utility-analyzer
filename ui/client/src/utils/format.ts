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
