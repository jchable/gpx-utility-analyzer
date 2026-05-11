import { useTranslation } from 'react-i18next';
import { Monitor, Sun, Moon } from 'lucide-react';
import { useTheme, type ThemeMode } from '../../hooks/useTheme';

const MODES: { mode: ThemeMode; icon: typeof Monitor }[] = [
  { mode: 'system', icon: Monitor },
  { mode: 'light', icon: Sun },
  { mode: 'dark', icon: Moon },
];

export default function ThemeSwitcher({ collapsed, mobile }: { collapsed?: boolean; mobile?: boolean }) {
  const { t } = useTranslation();
  const { mode, setMode } = useTheme();

  const currentIndex = MODES.findIndex((m) => m.mode === mode);
  const current = MODES[currentIndex];
  const Icon = current.icon;

  const handleToggle = () => {
    const nextIndex = (currentIndex + 1) % MODES.length;
    setMode(MODES[nextIndex].mode);
  };

  if (mobile) {
    return (
      <button
        onClick={handleToggle}
        className="flex flex-col items-center gap-0.5 px-2 py-1.5 rounded-lg text-xs font-medium text-content-muted hover:text-content transition-colors"
        title={t(`theme.${mode}`)}
      >
        <Icon size={20} />
        <span className="text-[10px]">{t(`theme.${mode}`)}</span>
      </button>
    );
  }

  return (
    <button
      onClick={handleToggle}
      className="flex items-center gap-2 px-3 py-2 rounded-lg text-sm text-content-muted hover:text-content hover:bg-surface-alt/50 transition-colors w-full"
      title={t(`theme.${mode}`)}
    >
      <Icon size={18} />
      {!collapsed && (
        <span className="text-xs font-medium">{t(`theme.${mode}`)}</span>
      )}
    </button>
  );
}
