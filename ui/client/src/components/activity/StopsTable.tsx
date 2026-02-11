import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { StopInfo, DurationValue } from '../../types/activity';
import { formatDuration } from '../../utils/format';

function formatTime(isoStart: string, activityStart: string, lang: string): string {
  const d = new Date(isoStart);
  // If valid date, show clock time
  if (!isNaN(d.getTime())) {
    return d.toLocaleTimeString(lang, { hour: '2-digit', minute: '2-digit', second: '2-digit' });
  }
  // Fallback: relative offset
  const offset = (new Date(isoStart).getTime() - new Date(activityStart).getTime()) / 1000;
  const h = Math.floor(offset / 3600);
  const m = Math.floor((offset % 3600) / 60);
  return h > 0 ? `+${h}h${String(m).padStart(2, '0')}` : `+${m}m`;
}

const COLLAPSE_THRESHOLD = 20;

interface Props {
  stops: StopInfo[];
  activityStartTime: string;
  totalStopTime: DurationValue;
  avgStopDuration: DurationValue;
  onStopClick?: (lat: number, lon: number) => void;
}

export default function StopsTable({ stops, activityStartTime, totalStopTime, avgStopDuration, onStopClick }: Props) {
  const { t } = useTranslation('activities');
  const { t: tc, i18n } = useTranslation();
  const [sortBy, setSortBy] = useState<'time' | 'duration'>('time');
  const [expanded, setExpanded] = useState(stops.length <= COLLAPSE_THRESHOLD);

  const sorted = [...stops].sort((a, b) => {
    if (sortBy === 'duration') return b.duration.seconds - a.duration.seconds;
    return new Date(a.start_time).getTime() - new Date(b.start_time).getTime();
  });

  return (
    <div className="bg-[#16213e] rounded-2xl border border-slate-700/50">
      <button
        onClick={() => setExpanded((v) => !v)}
        className="w-full flex items-center justify-between p-6 text-left cursor-pointer hover:bg-slate-800/20 transition-colors rounded-2xl"
      >
        <div className="flex items-center gap-3">
          <svg className="w-6 h-6 text-amber-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10 9v6m4-6v6m7-3a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          <h2 className="text-xl font-semibold text-white">
            {t('stopsTable.title')} ({stops.length})
          </h2>
          <span className="text-sm text-slate-400">{formatDuration(totalStopTime.seconds, tc)}</span>
        </div>
        <svg
          className={`w-5 h-5 text-slate-400 transition-transform duration-200 ${expanded ? 'rotate-180' : ''}`}
          fill="none"
          stroke="currentColor"
          viewBox="0 0 24 24"
        >
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
        </svg>
      </button>

      {expanded && (
        <div className="px-6 pb-6 space-y-4">
          <div className="flex justify-end">
            <div className="flex gap-1 bg-slate-800 rounded-lg p-0.5">
              <button
                onClick={() => setSortBy('time')}
                className={`px-3 py-1 text-xs rounded-md transition-colors ${sortBy === 'time' ? 'bg-slate-600 text-white' : 'text-slate-400 hover:text-white'}`}
              >
                {t('stopsTable.sortByTime')}
              </button>
              <button
                onClick={() => setSortBy('duration')}
                className={`px-3 py-1 text-xs rounded-md transition-colors ${sortBy === 'duration' ? 'bg-slate-600 text-white' : 'text-slate-400 hover:text-white'}`}
              >
                {t('stopsTable.sortByDuration')}
              </button>
            </div>
          </div>

          {stops.length > 0 ? (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-slate-500 text-xs uppercase border-b border-slate-700/50">
                    <th className="text-left py-2 px-2 w-10">{t('stopsTable.number')}</th>
                    <th className="text-left py-2 px-2">{t('stopsTable.startTime')}</th>
                    <th className="text-left py-2 px-2">{t('stopsTable.duration')}</th>
                    <th className="text-right py-2 px-2">{t('stopsTable.location')}</th>
                  </tr>
                </thead>
                <tbody>
                  {sorted.map((stop, i) => (
                    <tr
                      key={i}
                      className="border-b border-slate-700/30 hover:bg-slate-800/30 transition-colors cursor-pointer"
                      onClick={() => onStopClick?.(stop.lat, stop.lon)}
                    >
                      <td className="py-2.5 px-2 text-slate-500">{i + 1}</td>
                      <td className="py-2.5 px-2 text-slate-300">
                        {formatTime(stop.start_time, activityStartTime, i18n.language)}
                      </td>
                      <td className="py-2.5 px-2">
                        <span className="text-amber-400 font-medium">{formatDuration(stop.duration.seconds, tc)}</span>
                      </td>
                      <td className="py-2.5 px-2 text-right">
                        <button
                          onClick={(e) => { e.stopPropagation(); onStopClick?.(stop.lat, stop.lon); }}
                          className="text-cyan-400 hover:underline text-xs font-mono"
                          title={t('stopsTable.viewOnMap')}
                        >
                          {stop.lat.toFixed(4)}, {stop.lon.toFixed(4)}
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
                <tfoot>
                  <tr className="border-t border-slate-600/50 text-xs text-slate-400">
                    <td colSpan={2} className="py-2 px-2 font-medium">
                      {t('stopsTable.totalStops')}: {stops.length}
                    </td>
                    <td className="py-2 px-2">{formatDuration(totalStopTime.seconds, tc)}</td>
                    <td className="py-2 px-2 text-right">{t('stopsTable.avgDuration')}: {formatDuration(avgStopDuration.seconds, tc)}</td>
                  </tr>
                </tfoot>
              </table>
            </div>
          ) : (
            <p className="text-slate-500 text-sm">{t('stopsTable.noStops')}</p>
          )}
        </div>
      )}
    </div>
  );
}
