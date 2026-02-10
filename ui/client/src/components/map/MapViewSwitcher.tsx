import { Mountain, Satellite, Map } from 'lucide-react';
import { useTranslation } from 'react-i18next';

export type MapView = '3d-terrain' | '3d-satellite' | '2d-topo';

interface MapViewSwitcherProps {
  current: MapView;
  onChange: (view: MapView) => void;
}

const views: { id: MapView; labelKey: string; Icon: typeof Mountain }[] = [
  { id: '3d-terrain', labelKey: '3dTerrain', Icon: Mountain },
  { id: '3d-satellite', labelKey: 'satellite', Icon: Satellite },
  { id: '2d-topo', labelKey: 'topo2d', Icon: Map },
];

export default function MapViewSwitcher({ current, onChange }: MapViewSwitcherProps) {
  const { t } = useTranslation();

  return (
    <div className="absolute top-3 right-3 z-10 flex rounded-lg overflow-hidden border border-white/10 bg-[#0f0f1a]/90 backdrop-blur-sm shadow-lg">
      {views.map(({ id, labelKey, Icon }) => {
        const isActive = current === id;
        const label = t(`map.${labelKey}`);
        return (
          <button
            key={id}
            onClick={() => onChange(id)}
            title={label}
            className={`flex items-center gap-1.5 px-3 py-2 text-xs font-medium transition-colors ${
              isActive
                ? 'bg-[#00d4ff]/15 text-[#00d4ff]'
                : 'text-[#a0a0b0] hover:text-white hover:bg-white/5'
            } ${id !== '3d-terrain' ? 'border-l border-white/10' : ''}`}
          >
            <Icon size={16} />
            <span className="hidden sm:inline">{label}</span>
          </button>
        );
      })}
    </div>
  );
}
