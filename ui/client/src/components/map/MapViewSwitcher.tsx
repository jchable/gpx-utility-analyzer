import { Mountain, Satellite, Map } from 'lucide-react';

export type MapView = '3d-terrain' | '3d-satellite' | '2d-topo';

interface MapViewSwitcherProps {
  current: MapView;
  onChange: (view: MapView) => void;
}

const views: { id: MapView; label: string; Icon: typeof Mountain }[] = [
  { id: '3d-terrain', label: '3D Terrain', Icon: Mountain },
  { id: '3d-satellite', label: 'Satellite', Icon: Satellite },
  { id: '2d-topo', label: 'Topo 2D', Icon: Map },
];

export default function MapViewSwitcher({ current, onChange }: MapViewSwitcherProps) {
  return (
    <div className="absolute top-3 right-3 z-10 flex rounded-lg overflow-hidden border border-white/10 bg-[#0f0f1a]/90 backdrop-blur-sm shadow-lg">
      {views.map(({ id, label, Icon }) => {
        const isActive = current === id;
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
