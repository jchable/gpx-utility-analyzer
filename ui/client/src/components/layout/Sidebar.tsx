import { NavLink } from 'react-router-dom';
import {
  LayoutDashboard,
  Activity,
  Upload,
  Link,
  Settings,
  ChevronLeft,
  ChevronRight,
} from 'lucide-react';
import { useState } from 'react';

const navItems = [
  { to: '/', label: 'Dashboard', icon: LayoutDashboard },
  { to: '/activities', label: 'Activities', icon: Activity },
  { to: '/upload', label: 'Upload', icon: Upload },
  { to: '/integrations', label: 'Integrations', icon: Link },
  { to: '/settings', label: 'Settings', icon: Settings },
];

export default function Sidebar() {
  const [collapsed, setCollapsed] = useState(false);

  return (
    <>
      {/* Desktop sidebar */}
      <aside
        className={`hidden md:flex flex-col bg-[#0f0f1a] border-r border-white/5 transition-all duration-300 ${
          collapsed ? 'w-16' : 'w-64'
        } min-h-screen`}
      >
        {/* Logo / brand */}
        <div className="flex items-center justify-between h-16 px-4 border-b border-white/5">
          {!collapsed && (
            <span className="text-lg font-bold tracking-tight text-[#00d4ff]">
              GPX Analyzer
            </span>
          )}
          <button
            onClick={() => setCollapsed(!collapsed)}
            className="p-1.5 rounded-lg text-[#a0a0b0] hover:text-white hover:bg-white/5 transition-colors"
            aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
          >
            {collapsed ? <ChevronRight size={18} /> : <ChevronLeft size={18} />}
          </button>
        </div>

        {/* Navigation links */}
        <nav className="flex-1 flex flex-col gap-1 px-2 py-4">
          {navItems.map(({ to, label, icon: Icon }) => (
            <NavLink
              key={to}
              to={to}
              end={to === '/'}
              className={({ isActive }) =>
                `flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-colors ${
                  isActive
                    ? 'bg-[#00d4ff]/10 text-[#00d4ff]'
                    : 'text-[#a0a0b0] hover:text-white hover:bg-white/5'
                } ${collapsed ? 'justify-center' : ''}`
              }
            >
              {({ isActive }) => (
                <>
                  <Icon
                    size={20}
                    className={isActive ? 'text-[#00d4ff]' : ''}
                  />
                  {!collapsed && <span>{label}</span>}
                  {isActive && !collapsed && (
                    <span className="ml-auto w-1.5 h-1.5 rounded-full bg-[#00d4ff]" />
                  )}
                </>
              )}
            </NavLink>
          ))}
        </nav>

        {/* Footer */}
        {!collapsed && (
          <div className="px-4 py-3 border-t border-white/5">
            <p className="text-xs text-[#a0a0b0]/60">v0.1.0</p>
          </div>
        )}
      </aside>

      {/* Mobile bottom navigation */}
      <nav className="md:hidden fixed bottom-0 left-0 right-0 z-50 bg-[#0f0f1a] border-t border-white/5 flex items-center justify-around px-2 py-1.5 safe-bottom">
        {navItems.map(({ to, label, icon: Icon }) => (
          <NavLink
            key={to}
            to={to}
            end={to === '/'}
            className={({ isActive }) =>
              `flex flex-col items-center gap-0.5 px-2 py-1.5 rounded-lg text-[10px] font-medium transition-colors ${
                isActive
                  ? 'text-[#00d4ff]'
                  : 'text-[#a0a0b0] hover:text-white'
              }`
            }
          >
            {({ isActive }) => (
              <>
                <Icon
                  size={20}
                  className={isActive ? 'text-[#00d4ff]' : ''}
                />
                <span>{label}</span>
              </>
            )}
          </NavLink>
        ))}
      </nav>
    </>
  );
}
