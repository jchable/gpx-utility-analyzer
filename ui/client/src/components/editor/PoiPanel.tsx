import { useTranslation } from 'react-i18next';
import { MapPin, Trash2 } from 'lucide-react';
import { useEditorStore } from '../../stores/editorStore';
import type { PoiType } from '../../types/route';
import { POI_TYPE_CONFIG } from '../../constants/poi';

interface PoiPanelProps {
  selectedPoiType: PoiType;
  onPoiTypeChange: (type: PoiType) => void;
}

export default function PoiPanel({ selectedPoiType, onPoiTypeChange }: PoiPanelProps) {
  const { t } = useTranslation('routes');

  const pois = useEditorStore((s) => s.pois);
  const selectedPoiId = useEditorStore((s) => s.selectedPoiId);
  const selectPoi = useEditorStore((s) => s.selectPoi);
  const deletePoi = useEditorStore((s) => s.deletePoi);
  const updatePoi = useEditorStore((s) => s.updatePoi);
  const mode = useEditorStore((s) => s.mode);

  // Only show when in addPoi mode or when there are existing POIs
  if (mode !== 'addPoi' && pois.length === 0) return null;

  return (
    <div className="absolute left-16 top-3 z-10 w-56 bg-surface/95 backdrop-blur-sm border border-border rounded-lg shadow-lg overflow-hidden">
      {/* POI type selector (visible in addPoi mode) */}
      {mode === 'addPoi' && (
        <div className="p-2 border-b border-border">
          <span className="text-[9px] font-medium text-content-muted uppercase tracking-wider px-1">
            {t('poi.custom')}
          </span>
          <div className="grid grid-cols-3 gap-1 mt-1">
            {POI_TYPE_CONFIG.map(({ id, icon: Icon, color }) => (
              <button
                key={id}
                onClick={() => onPoiTypeChange(id)}
                title={t(`poi.${id}`)}
                className={`flex flex-col items-center gap-0.5 px-1 py-1.5 rounded text-[9px] transition-colors ${
                  selectedPoiType === id
                    ? 'bg-surface-alt/50 ring-1 ring-border'
                    : 'hover:bg-surface-alt/30'
                }`}
              >
                <Icon size={14} style={{ color }} />
                <span className="text-content-muted truncate w-full text-center">{t(`poi.${id}`)}</span>
              </button>
            ))}
          </div>
        </div>
      )}

      {/* POI list */}
      {pois.length > 0 && (
        <div className="max-h-48 overflow-y-auto">
          {pois.map((poi) => {
            const typeInfo = POI_TYPE_CONFIG.find((p) => p.id === poi.type);
            const Icon = typeInfo?.icon ?? MapPin;
            const isSelected = selectedPoiId === poi.id;

            return (
              <div
                key={poi.id}
                onClick={() => selectPoi(isSelected ? null : poi.id)}
                className={`flex items-center gap-2 px-3 py-2 cursor-pointer transition-colors ${
                  isSelected ? 'bg-surface-alt/50' : 'hover:bg-surface-alt/30'
                }`}
              >
                <Icon size={14} style={{ color: typeInfo?.color ?? '#6b7280' }} />
                <div className="flex-1 min-w-0">
                  {isSelected ? (
                    <input
                      type="text"
                      value={poi.name}
                      onChange={(e) => updatePoi(poi.id, { name: e.target.value })}
                      onClick={(e) => e.stopPropagation()}
                      className="w-full bg-transparent text-content text-xs border-b border-border outline-none"
                    />
                  ) : (
                    <span className="text-xs text-content truncate block">{poi.name}</span>
                  )}
                </div>
                {isSelected && (
                  <button
                    onClick={(e) => {
                      e.stopPropagation();
                      deletePoi(poi.id);
                    }}
                    className="text-red-400 hover:text-red-300 transition-colors"
                  >
                    <Trash2 size={12} />
                  </button>
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
