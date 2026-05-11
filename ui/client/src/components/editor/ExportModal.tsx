import { useTranslation } from 'react-i18next';
import { FileDown } from 'lucide-react';
import { routesApi } from '../../api/routes-client';

interface ExportModalProps {
  routeId: string;
  onClose: () => void;
}

const FORMATS: { id: 'gpx' | 'geojson' | 'kml'; mime: string }[] = [
  { id: 'gpx', mime: 'application/gpx+xml' },
  { id: 'geojson', mime: 'application/geo+json' },
  { id: 'kml', mime: 'application/vnd.google-earth.kml+xml' },
];

export default function ExportModal({ routeId, onClose }: ExportModalProps) {
  const { t } = useTranslation('routes');

  const handleExport = (format: 'gpx' | 'geojson' | 'kml') => {
    const url = routesApi.getExportUrl(routeId, format);
    window.open(url, '_blank');
    onClose();
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm">
      <div className="bg-surface-card border border-border rounded-xl shadow-2xl p-6 max-w-xs w-full mx-4">
        <h3 className="text-sm font-semibold text-content mb-4">{t('export.title')}</h3>

        <div className="flex flex-col gap-2">
          {FORMATS.map(({ id }) => (
            <button
              key={id}
              onClick={() => handleExport(id)}
              className="w-full flex items-center gap-2 px-4 py-2.5 text-xs font-medium text-white bg-accent/15 hover:bg-accent/25 border border-accent/30 rounded-lg transition-colors"
            >
              <FileDown size={14} />
              {t(`export.${id}`)}
            </button>
          ))}
        </div>

        <button
          onClick={onClose}
          className="w-full mt-3 px-4 py-2 text-xs text-content-muted hover:text-content hover:bg-surface-alt/30 rounded-lg transition-colors"
        >
          {t('editor.discard')}
        </button>
      </div>
    </div>
  );
}
