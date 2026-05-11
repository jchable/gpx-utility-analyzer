import { useTranslation } from 'react-i18next';
import { Globe } from 'lucide-react';

const LANGUAGES = [
  { code: 'en', flag: '🇬🇧' },
  { code: 'fr', flag: '🇫🇷' },
] as const;

export default function LanguageSwitcher({ collapsed, mobile }: { collapsed?: boolean; mobile?: boolean }) {
  const { i18n } = useTranslation();
  const current = LANGUAGES.find((l) => i18n.language.startsWith(l.code)) ?? LANGUAGES[0];
  const next = LANGUAGES.find((l) => l.code !== current.code) ?? LANGUAGES[1];

  const handleToggle = () => {
    i18n.changeLanguage(next.code);
  };

  if (mobile) {
    return (
      <button
        onClick={handleToggle}
        className="flex flex-col items-center gap-0.5 px-2 py-1.5 rounded-lg text-xs font-medium text-content-muted hover:text-content transition-colors"
        title={`Switch to ${next.code.toUpperCase()}`}
      >
        <Globe size={20} />
        <span>{current.code.toUpperCase()}</span>
      </button>
    );
  }

  return (
    <button
      onClick={handleToggle}
      className="flex items-center gap-2 px-3 py-2 rounded-lg text-sm text-content-muted hover:text-content hover:bg-surface-alt/50 transition-colors w-full"
      title={`Switch to ${next.code.toUpperCase()}`}
    >
      <Globe size={18} />
      {!collapsed && (
        <span className="text-xs font-medium">{current.code.toUpperCase()}</span>
      )}
    </button>
  );
}
