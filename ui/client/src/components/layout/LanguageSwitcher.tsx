import { useTranslation } from 'react-i18next';
import { Globe } from 'lucide-react';

const LANGUAGES = [
  { code: 'en', flag: '🇬🇧' },
  { code: 'fr', flag: '🇫🇷' },
] as const;

export default function LanguageSwitcher({ collapsed }: { collapsed?: boolean }) {
  const { i18n } = useTranslation();
  const current = LANGUAGES.find((l) => i18n.language.startsWith(l.code)) ?? LANGUAGES[0];
  const next = LANGUAGES.find((l) => l.code !== current.code) ?? LANGUAGES[1];

  const handleToggle = () => {
    i18n.changeLanguage(next.code);
  };

  return (
    <button
      onClick={handleToggle}
      className="flex items-center gap-2 px-3 py-2 rounded-lg text-sm text-[#a0a0b0] hover:text-white hover:bg-white/5 transition-colors w-full"
      title={`Switch to ${next.code.toUpperCase()}`}
    >
      <Globe size={18} />
      {!collapsed && (
        <span className="text-xs font-medium">{current.code.toUpperCase()}</span>
      )}
    </button>
  );
}
