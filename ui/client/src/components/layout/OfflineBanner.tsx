import { WifiOff } from 'lucide-react';
import { useOnlineStatus } from '../../hooks/useOnlineStatus';

export default function OfflineBanner() {
  const isOnline = useOnlineStatus();

  if (isOnline) return null;

  return (
    <div className="bg-amber-600/90 text-white text-sm px-4 py-2 flex items-center justify-center gap-2">
      <WifiOff size={16} />
      <span>You are offline — showing cached data</span>
    </div>
  );
}
