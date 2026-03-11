import { NavLink } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  LayoutDashboard,
  Activity,
  Upload,
  Map,
  Route,
  Link,
  Settings,
  ChevronLeft,
  ChevronRight,
} from 'lucide-react';
import { useState } from 'react';
import LanguageSwitcher from './LanguageSwitcher';
import ThemeSwitcher from './ThemeSwitcher';

const navItems = [
  { to: '/', labelKey: 'nav.dashboard', icon: LayoutDashboard },
  { to: '/activities', labelKey: 'nav.activities', icon: Activity },
  { to: '/upload', labelKey: 'nav.upload', icon: Upload },
  { to: '/predict', labelKey: 'nav.predict', icon: Map },
  { to: '/routes', labelKey: 'nav.routes', icon: Route },
  { to: '/integrations', labelKey: 'nav.integrations', icon: Link },
  { to: '/settings', labelKey: 'nav.settings', icon: Settings },
];

export default function Sidebar() {
  const [collapsed, setCollapsed] = useState(false);
  const { t } = useTranslation();

  return (
    <>
      {/* Desktop sidebar */}
      <aside
        className={`hidden md:flex flex-col bg-surface border-r border-border transition-all duration-300 ${
          collapsed ? 'w-16' : 'w-64'
        } h-full`}
      >
        {/* Logo / brand */}
        <div className="flex items-center justify-between h-16 px-4 border-b border-border">
          {!collapsed && (
            <span className="text-lg font-bold tracking-tight text-accent">
              {t('appName')}
            </span>
          )}
          <button
            onClick={() => setCollapsed(!collapsed)}
            className="p-1.5 rounded-lg text-content-muted hover:text-content hover:bg-surface-alt/50 transition-colors"
            aria-label={collapsed ? t('sidebar.expand') : t('sidebar.collapse')}
          >
            {collapsed ? <ChevronRight size={18} /> : <ChevronLeft size={18} />}
          </button>
        </div>

        {/* Navigation links */}
        <nav className="flex-1 flex flex-col gap-1 px-2 py-4">
          {navItems.map(({ to, labelKey, icon: Icon }) => (
            <NavLink
              key={to}
              to={to}
              end={to === '/'}
              className={({ isActive }) =>
                `flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-colors ${
                  isActive
                    ? 'bg-accent/10 text-accent'
                    : 'text-content-muted hover:text-content hover:bg-surface-alt/50'
                } ${collapsed ? 'justify-center' : ''}`
              }
            >
              {({ isActive }) => (
                <>
                  <Icon
                    size={20}
                    className={isActive ? 'text-accent' : ''}
                  />
                  {!collapsed && <span>{t(labelKey)}</span>}
                  {isActive && !collapsed && (
                    <span className="ml-auto w-1.5 h-1.5 rounded-full bg-accent" />
                  )}
                </>
              )}
            </NavLink>
          ))}
        </nav>

        {/* Footer */}
        <div className="px-2 py-2 border-t border-border">
          <ThemeSwitcher collapsed={collapsed} />
          <LanguageSwitcher collapsed={collapsed} />
          {!collapsed && (
            <p className="text-xs text-content-muted/60 px-3 py-1">v0.1.0</p>
          )}
        </div>
      </aside>

      {/* Mobile bottom navigation */}
      <nav className="md:hidden fixed bottom-0 left-0 right-0 z-50 bg-surface border-t border-border flex items-center justify-around px-2 py-1.5 safe-bottom">
        {navItems.map(({ to, labelKey, icon: Icon }) => (
          <NavLink
            key={to}
            to={to}
            end={to === '/'}
            className={({ isActive }) =>
              `flex flex-col items-center gap-0.5 px-2 py-1.5 rounded-lg text-xs font-medium transition-colors ${
                isActive
                  ? 'text-accent'
                  : 'text-content-muted hover:text-content'
              }`
            }
          >
            {({ isActive }) => (
              <>
                <Icon
                  size={20}
                  className={isActive ? 'text-accent' : ''}
                />
                <span>{t(labelKey)}</span>
              </>
            )}
          </NavLink>
        ))}
        <ThemeSwitcher mobile />
        <LanguageSwitcher mobile />
      </nav>
    </>
  );
}
