import { useTranslation } from 'react-i18next';
import { PanelRightClose, PanelRightOpen } from 'lucide-react';
import { useEditorStore } from '../../stores/editorStore';
import type { RouteCategory } from '../../types/route';

const ACTIVITY_TYPES = ['trail', 'hike', 'run', 'cycle', 'walk', 'other'] as const;
const ROUTE_CATEGORIES: RouteCategory[] = ['loop', 'out-and-back', 'traverse', 'point-to-point'];

interface MetadataPanelProps {
  collapsed?: boolean;
  onToggle?: () => void;
}

export default function MetadataPanel({ collapsed = false, onToggle }: MetadataPanelProps) {
  const { t } = useTranslation('routes');
  const { t: tc } = useTranslation();

  const routeName = useEditorStore((s) => s.routeName);
  const routeDescription = useEditorStore((s) => s.routeDescription);
  const activityType = useEditorStore((s) => s.activityType);
  const routeCategory = useEditorStore((s) => s.routeCategory);
  const tags = useEditorStore((s) => s.tags);
  const setRouteName = useEditorStore((s) => s.setRouteName);
  const setRouteDescription = useEditorStore((s) => s.setRouteDescription);
  const setActivityType = useEditorStore((s) => s.setActivityType);
  const setRouteCategory = useEditorStore((s) => s.setRouteCategory);
  const setTags = useEditorStore((s) => s.setTags);

  // Collapsed: just a toggle button
  if (collapsed) {
    return (
      <button
        onClick={onToggle}
        className="absolute right-3 top-3 z-10 flex items-center justify-center w-10 h-10 rounded-lg border border-white/10 bg-[#0f0f1a]/90 backdrop-blur-sm shadow-lg text-[#a0a0b0] hover:text-white hover:bg-white/5 transition-colors"
        title={t('metadata.name')}
      >
        <PanelRightOpen size={18} />
      </button>
    );
  }

  return (
    <div className="absolute right-0 top-0 bottom-0 z-10 w-72 flex flex-col bg-[#0f0f1a]/95 backdrop-blur-sm border-l border-white/10 shadow-lg overflow-y-auto">
      {/* Header with collapse button */}
      <div className="flex items-center justify-between px-4 py-3 border-b border-white/5">
        <span className="text-xs font-semibold text-white uppercase tracking-wider">{t('metadata.name')}</span>
        <button
          onClick={onToggle}
          className="text-[#a0a0b0] hover:text-white transition-colors"
        >
          <PanelRightClose size={16} />
        </button>
      </div>

      <div className="flex flex-col gap-4 p-4">
        {/* Route name */}
        <div>
          <label className="block text-[10px] font-medium text-[#a0a0b0] uppercase tracking-wider mb-1">
            {t('metadata.name')}
          </label>
          <input
            type="text"
            value={routeName}
            onChange={(e) => setRouteName(e.target.value)}
            placeholder={t('metadata.name')}
            className="w-full px-3 py-2 text-sm bg-[#16213e] border border-white/10 rounded-lg text-white placeholder-[#a0a0b0]/50 focus:outline-none focus:border-[#00d4ff]/50 transition-colors"
          />
        </div>

        {/* Description */}
        <div>
          <label className="block text-[10px] font-medium text-[#a0a0b0] uppercase tracking-wider mb-1">
            {t('metadata.description')}
          </label>
          <textarea
            value={routeDescription}
            onChange={(e) => setRouteDescription(e.target.value)}
            placeholder={t('metadata.description')}
            rows={3}
            className="w-full px-3 py-2 text-sm bg-[#16213e] border border-white/10 rounded-lg text-white placeholder-[#a0a0b0]/50 focus:outline-none focus:border-[#00d4ff]/50 transition-colors resize-none"
          />
        </div>

        {/* Activity type */}
        <div>
          <label className="block text-[10px] font-medium text-[#a0a0b0] uppercase tracking-wider mb-1">
            {t('metadata.activityType')}
          </label>
          <select
            value={activityType}
            onChange={(e) => setActivityType(e.target.value)}
            className="w-full px-3 py-2 text-sm bg-[#16213e] border border-white/10 rounded-lg text-white focus:outline-none focus:border-[#00d4ff]/50 transition-colors"
          >
            {ACTIVITY_TYPES.map((type) => (
              <option key={type} value={type}>
                {tc(`activityType.${type}`)}
              </option>
            ))}
          </select>
        </div>

        {/* Route category */}
        <div>
          <label className="block text-[10px] font-medium text-[#a0a0b0] uppercase tracking-wider mb-1">
            {t('metadata.category')}
          </label>
          <select
            value={routeCategory}
            onChange={(e) => setRouteCategory(e.target.value)}
            className="w-full px-3 py-2 text-sm bg-[#16213e] border border-white/10 rounded-lg text-white focus:outline-none focus:border-[#00d4ff]/50 transition-colors"
          >
            <option value="">{t('allTypes')}</option>
            {ROUTE_CATEGORIES.map((cat) => (
              <option key={cat} value={cat}>
                {t(`category.${cat}`)}
              </option>
            ))}
          </select>
        </div>

        {/* Tags */}
        <div>
          <label className="block text-[10px] font-medium text-[#a0a0b0] uppercase tracking-wider mb-1">
            {t('metadata.tags')}
          </label>
          <input
            type="text"
            value={tags}
            onChange={(e) => setTags(e.target.value)}
            placeholder="tag1, tag2, ..."
            className="w-full px-3 py-2 text-sm bg-[#16213e] border border-white/10 rounded-lg text-white placeholder-[#a0a0b0]/50 focus:outline-none focus:border-[#00d4ff]/50 transition-colors"
          />
          <span className="text-[9px] text-[#a0a0b0] mt-1 block">{t('metadata.tags')} (comma-separated)</span>
        </div>
      </div>
    </div>
  );
}
