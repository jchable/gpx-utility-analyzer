import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { LogOut, User } from 'lucide-react';
import { useAuth } from '../../contexts/AuthContext';

export default function UserMenu({ collapsed = false }: { collapsed?: boolean }) {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const { t } = useTranslation('auth');

  if (!user) return null;

  const handleLogout = async () => {
    await logout();
    navigate('/login');
  };

  const initials = user.displayName
    .split(' ')
    .map(n => n[0])
    .join('')
    .slice(0, 2)
    .toUpperCase();

  if (collapsed) {
    return (
      <button
        onClick={handleLogout}
        className="w-full flex justify-center p-2 rounded-lg text-content-muted hover:text-content hover:bg-surface-alt/50 transition-colors"
        title={t('logout')}
      >
        <LogOut size={18} />
      </button>
    );
  }

  return (
    <div className="flex items-center gap-3 px-3 py-2">
      <div className="flex-shrink-0 w-8 h-8 rounded-full bg-accent/20 text-accent flex items-center justify-center text-xs font-bold">
        {initials || <User size={14} />}
      </div>
      <div className="flex-1 min-w-0">
        <p className="text-sm font-medium text-content truncate">{user.displayName}</p>
        <p className="text-xs text-content-muted truncate">{user.email}</p>
      </div>
      <button
        onClick={handleLogout}
        className="p-1.5 rounded-lg text-content-muted hover:text-content hover:bg-surface-alt/50 transition-colors"
        title={t('logout')}
      >
        <LogOut size={16} />
      </button>
    </div>
  );
}
