import { useTranslation } from 'react-i18next';
import { useEditorStore } from '../../stores/editorStore';

interface SplitModalProps {
  splitIndex: number;
  onClose: () => void;
  onCreateSecondRoute?: (coords: number[][]) => void;
}

export default function SplitModal({ splitIndex, onClose, onCreateSecondRoute }: SplitModalProps) {
  const { t } = useTranslation('routes');
  const routeCoordinates = useEditorStore((s) => s.routeCoordinates);
  const splitRouteAt = useEditorStore((s) => s.splitRouteAt);
  const setRouteCoordinates = useEditorStore((s) => s.setRouteCoordinates);

  const firstPartLength = splitIndex + 1;
  const secondPartLength = routeCoordinates.length - splitIndex;

  const handleKeepFirst = () => {
    splitRouteAt(splitIndex);
    onClose();
  };

  const handleKeepSecond = () => {
    const secondPart = routeCoordinates.slice(splitIndex);
    setRouteCoordinates(secondPart);
    onClose();
  };

  const handleKeepBoth = () => {
    const secondPart = splitRouteAt(splitIndex);
    if (secondPart.length > 0 && onCreateSecondRoute) {
      onCreateSecondRoute(secondPart);
    }
    onClose();
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm">
      <div className="bg-[#16213e] border border-white/10 rounded-xl shadow-2xl p-6 max-w-sm w-full mx-4">
        <h3 className="text-sm font-semibold text-white mb-4">{t('split.title')}</h3>

        <p className="text-xs text-[#a0a0b0] mb-4">
          {t('split.point', { index: splitIndex, total: routeCoordinates.length - 1 })}
        </p>

        <div className="flex flex-col gap-2">
          <button
            onClick={handleKeepFirst}
            className="w-full px-4 py-2 text-xs font-medium text-white bg-[#00d4ff]/15 hover:bg-[#00d4ff]/25 border border-[#00d4ff]/30 rounded-lg transition-colors text-left"
          >
            {t('split.keepFirst')} ({firstPartLength} pts)
          </button>

          <button
            onClick={handleKeepSecond}
            className="w-full px-4 py-2 text-xs font-medium text-white bg-[#00d4ff]/15 hover:bg-[#00d4ff]/25 border border-[#00d4ff]/30 rounded-lg transition-colors text-left"
          >
            {t('split.keepSecond')} ({secondPartLength} pts)
          </button>

          <button
            onClick={handleKeepBoth}
            className="w-full px-4 py-2 text-xs font-medium text-white bg-purple-500/15 hover:bg-purple-500/25 border border-purple-500/30 rounded-lg transition-colors text-left"
          >
            {t('split.keepBoth')}
          </button>
        </div>

        <button
          onClick={onClose}
          className="w-full mt-3 px-4 py-2 text-xs text-[#a0a0b0] hover:text-white hover:bg-white/5 rounded-lg transition-colors"
        >
          {t('editor.discard')}
        </button>
      </div>
    </div>
  );
}
