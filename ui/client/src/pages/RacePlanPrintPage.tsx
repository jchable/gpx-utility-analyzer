import { useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useEffect } from 'react';
import { useRacePlan } from '../hooks/useRacePlans';
import { formatArrivalTime, formatElapsedTime } from '../utils/dayNight';
import { formatDate, formatPageDuration } from '../utils/format';

const CHECKPOINT_ICONS: Record<string, string> = {
  start: '🟢',
  finish: '🔴',
  aid_station: '🔵',
  checkpoint: '🟡',
  crew_only: '🟣',
};

export default function RacePlanPrintPage() {
  const { id } = useParams<{ id: string }>();
  const { t } = useTranslation('race-plans');
  const { t: tc } = useTranslation();
  const { i18n } = useTranslation();
  const { data: plan, isLoading } = useRacePlan(id!);

  // Auto-trigger print dialog after load
  useEffect(() => {
    if (plan) {
      const timeout = setTimeout(() => window.print(), 500);
      return () => clearTimeout(timeout);
    }
  }, [plan]);

  if (isLoading || !plan) {
    return (
      <div className="flex items-center justify-center h-screen">
        <div className="animate-spin rounded-full h-8 w-8 border-t-2 border-b-2 border-gray-400" />
      </div>
    );
  }

  const startTime = plan.startTime ?? '00:00';
  const sorted = [...plan.checkpoints].sort((a, b) => a.order - b.order);
  const equipment = plan.equipment ?? [];
  const mandatoryEquipment = equipment.filter((e) => e.isMandatory);

  return (
    <>
      {/* Print styles injected inline */}
      <style>{`
        @media print {
          @page { size: A4 portrait; margin: 15mm; }
          body { color: black !important; background: white !important; }
          .no-print { display: none !important; }
        }
        body { font-family: sans-serif; font-size: 12px; color: #111; background: white; }
        table { border-collapse: collapse; width: 100%; }
        th, td { border: 1px solid #ccc; padding: 4px 8px; text-align: left; }
        th { background: #f3f4f6; font-weight: 600; }
        .section { margin-bottom: 16px; }
        .section-title { font-weight: 700; font-size: 14px; border-bottom: 2px solid #111; padding-bottom: 4px; margin-bottom: 8px; }
        .header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 16px; }
        .plan-title { font-size: 20px; font-weight: 800; }
        .plan-meta { color: #555; font-size: 11px; }
        .badge { display: inline-block; font-weight: 600; font-size: 11px; }
        .night { background: #e8ecf5; }
        .late { color: #dc2626; font-weight: 700; }
        .crew { color: #7c3aed; }
        .mandatory { font-weight: 700; }
        .checkbox { display: inline-block; width: 14px; height: 14px; border: 1px solid #888; margin-right: 4px; vertical-align: middle; }
      `}</style>

      <div className="p-4 max-w-[210mm] mx-auto">
        {/* Header */}
        <div className="header">
          <div>
            <div className="plan-title">{plan.name}</div>
            {plan.description && <div className="plan-meta" style={{ marginTop: 2 }}>{plan.description}</div>}
            <div className="plan-meta" style={{ marginTop: 4 }}>
              {plan.raceDate && formatDate(plan.raceDate, i18n.language)}
              {plan.startTime && ` · ${t('timing.startTime')}: ${plan.startTime}`}
            </div>
          </div>
          <div style={{ textAlign: 'right' }}>
            <div><strong>{plan.distanceKm.toFixed(1)} km</strong></div>
            <div>+{Math.round(plan.elevationGainM)} m / -{Math.round(plan.elevationLossM)} m</div>
            {plan.targetTimeSeconds && (
              <div>{t('stats.objectiveA')}: <strong>{formatPageDuration(plan.targetTimeSeconds, tc)}</strong></div>
            )}
            {plan.targetTimeBSeconds && (
              <div>{t('stats.objectiveB')}: {formatPageDuration(plan.targetTimeBSeconds, tc)}</div>
            )}
            {plan.targetTimeCSeconds && (
              <div>{t('stats.objectiveC')}: {formatPageDuration(plan.targetTimeCSeconds, tc)}</div>
            )}
          </div>
        </div>

        {/* Checkpoints */}
        <div className="section">
          <div className="section-title">{t('tabs.timeline')}</div>
          <table>
            <thead>
              <tr>
                <th>{t('checkpoint.name')}</th>
                <th style={{ textAlign: 'right' }}>km</th>
                <th style={{ textAlign: 'right' }}>D+</th>
                <th style={{ textAlign: 'right' }}>{t('timing.arrivalTime')}</th>
                <th style={{ textAlign: 'right' }}>{t('timing.elapsedTime')}</th>
                <th style={{ textAlign: 'right' }}>{t('timing.cutoffTime')}</th>
                <th style={{ textAlign: 'right' }}>{t('checkpoint.pause')}</th>
                <th>Notes</th>
              </tr>
            </thead>
            <tbody>
              {sorted.map((cp) => {
                const arrival = cp.targetArrivalSeconds != null
                  ? formatArrivalTime(startTime, cp.targetArrivalSeconds)
                  : '—';
                const elapsed = cp.targetArrivalSeconds != null
                  ? formatElapsedTime(cp.targetArrivalSeconds)
                  : '—';
                const cutoff = cp.cutoffTimeSeconds != null
                  ? formatArrivalTime(startTime, cp.cutoffTimeSeconds)
                  : '—';
                const pause = cp.plannedPauseSeconds != null && cp.plannedPauseSeconds > 0
                  ? formatElapsedTime(cp.plannedPauseSeconds)
                  : '—';
                const isLate = cp.targetArrivalSeconds != null && cp.cutoffTimeSeconds != null &&
                  cp.targetArrivalSeconds > cp.cutoffTimeSeconds - 600;
                const icon = CHECKPOINT_ICONS[cp.type] ?? '⚪';

                const notes = [];
                if (cp.isCrewAccessible) notes.push(`👥 ${cp.crewNotes ?? 'Crew'}`);
                if (cp.hasDropBag) notes.push('🎒 Drop bag');

                return (
                  <tr key={cp.id}>
                    <td>{icon} {cp.name}</td>
                    <td style={{ textAlign: 'right' }}>{cp.distanceKm.toFixed(1)}</td>
                    <td style={{ textAlign: 'right' }}>—</td>
                    <td style={{ textAlign: 'right' }} className={isLate ? 'late' : ''}>{arrival}</td>
                    <td style={{ textAlign: 'right' }}>{elapsed}</td>
                    <td style={{ textAlign: 'right' }} className={isLate ? 'late' : ''}>{cutoff}</td>
                    <td style={{ textAlign: 'right' }}>{pause}</td>
                    <td className={cp.isCrewAccessible ? 'crew' : ''}>
                      {notes.join(' · ') || (cp.notes ?? '')}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>

        {/* Mandatory equipment */}
        {mandatoryEquipment.length > 0 && (
          <div className="section">
            <div className="section-title">{t('equipment.title')}</div>
            <div style={{ columns: 2, gap: 16 }}>
              {mandatoryEquipment.map((item, i) => (
                <div key={i} style={{ marginBottom: 4 }}>
                  <span className="checkbox" />
                  <span className="mandatory">{item.name}</span>
                  {item.notes && <span style={{ color: '#555' }}> — {item.notes}</span>}
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Print action (no-print) */}
        <div className="no-print" style={{ textAlign: 'center', marginTop: 24 }}>
          <button
            onClick={() => window.print()}
            style={{ padding: '8px 24px', background: '#0891b2', color: 'white', borderRadius: 8, border: 'none', cursor: 'pointer', fontWeight: 600 }}
          >
            {t('print.print')}
          </button>
        </div>
      </div>
    </>
  );
}
