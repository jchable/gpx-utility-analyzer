import { Outlet } from 'react-router-dom';
import Sidebar from './Sidebar';
import OfflineBanner from './OfflineBanner';

export default function Layout() {
  return (
    <div className="flex h-screen overflow-hidden">
      {/* Sidebar (desktop: fixed left column, mobile: bottom nav rendered inside Sidebar) */}
      <Sidebar />

      {/* Main content area — scrolls independently from sidebar */}
      <div className="flex-1 flex flex-col min-h-0 overflow-y-auto">
        <OfflineBanner />
        <main className="flex-1 bg-surface-alt pb-20 md:pb-0">
          <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-6">
            <Outlet />
          </div>
        </main>
      </div>
    </div>
  );
}
