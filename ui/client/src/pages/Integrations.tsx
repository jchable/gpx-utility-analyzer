import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useIntegrations } from '../hooks/useActivities';
import { api } from '../api/client';
import type { IntegrationInfo } from '../types/activity';

const PROVIDER_STYLE: Record<string, { color: string; icon: string }> = {
  strava: { color: '#FC4C02', icon: 'S' },
  garmin: { color: '#007CC3', icon: 'G' },
  coros: { color: '#00D4AA', icon: 'C' },
  suunto: { color: '#E4032E', icon: 'S' },
  polar: { color: '#D0021B', icon: 'P' },
  komoot: { color: '#6AA127', icon: 'K' },
};

function IntegrationCard({
  integration,
  onConnect,
  onDisconnect,
}: {
  integration: IntegrationInfo;
  onConnect: () => void;
  onDisconnect: () => void;
}) {
  const [loading, setLoading] = useState(false);
  const { t } = useTranslation('integrations');
  const { t: tc } = useTranslation();
  const { i18n } = useTranslation();

  const style = PROVIDER_STYLE[integration.provider] || {
    color: '#888888',
    icon: integration.provider.charAt(0).toUpperCase(),
  };

  const providerName = t(`provider.${integration.provider}.name`, { defaultValue: integration.provider });
  const providerDescription = t(`provider.${integration.provider}.description`, { defaultValue: t('defaultDescription') });

  const handleAction = async () => {
    setLoading(true);
    try {
      if (integration.isConnected) {
        await onDisconnect();
      } else {
        await onConnect();
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="bg-[#16213e] rounded-2xl p-6 border border-slate-700/50 hover:border-slate-600 transition-colors">
      <div className="flex items-start gap-4">
        {/* Provider Icon */}
        <div
          className="w-12 h-12 rounded-xl flex items-center justify-center text-lg font-bold text-white shrink-0"
          style={{ backgroundColor: style.color + '33', color: style.color }}
        >
          {style.icon}
        </div>

        {/* Info */}
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 mb-1">
            <h3 className="text-white font-semibold">{providerName}</h3>
            {integration.isConnected && (
              <span className="text-xs font-medium px-2 py-0.5 rounded-full bg-green-500/20 text-green-400">
                {t('connected')}
              </span>
            )}
          </div>
          <p className="text-sm text-slate-400 mb-4">{providerDescription}</p>

          {/* Connected details */}
          {integration.isConnected && integration.externalUserId && (
            <p className="text-xs text-slate-500 mb-3">
              {t('account', { userId: integration.externalUserId })}
              {integration.connectedAt && (
                <> -- {t('connected')} {new Date(integration.connectedAt).toLocaleDateString(i18n.language)}</>
              )}
            </p>
          )}

          {/* Action */}
          <button
            onClick={handleAction}
            disabled={loading}
            className={`px-4 py-2 rounded-lg text-sm font-medium transition-colors disabled:opacity-50 ${
              integration.isConnected
                ? 'bg-red-600/20 border border-red-800 text-red-400 hover:bg-red-600/30'
                : 'text-white hover:opacity-90'
            }`}
            style={
              !integration.isConnected
                ? { backgroundColor: style.color }
                : undefined
            }
          >
            {loading
              ? t('processing')
              : integration.isConnected
                ? tc('button.disconnect')
                : tc('button.connect')}
          </button>
        </div>
      </div>
    </div>
  );
}

export default function Integrations() {
  const { data: integrations, isLoading, error, refetch } = useIntegrations();
  const { t } = useTranslation('integrations');

  const handleConnect = async (provider: string) => {
    await api.connectIntegration(provider);
    // connectIntegration redirects to OAuth, so refetch is for when user returns
  };

  const handleDisconnect = async (provider: string) => {
    await api.disconnectIntegration(provider);
    refetch();
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-96">
        <div className="animate-spin rounded-full h-12 w-12 border-t-2 border-b-2 border-cyan-400" />
      </div>
    );
  }

  if (error) {
    return (
      <div className="flex items-center justify-center h-96">
        <p className="text-red-400 text-lg">{t('loadError', { message: error.message })}</p>
      </div>
    );
  }

  // Group into connected and available
  const connected = integrations?.filter((i) => i.isConnected) || [];
  const available = integrations?.filter((i) => !i.isConnected) || [];

  return (
    <div className="space-y-8">
      {/* Header */}
      <div>
        <h1 className="text-3xl font-bold text-white tracking-tight">{t('title')}</h1>
        <p className="text-slate-400 mt-1">
          {t('subtitle')}
        </p>
      </div>

      {/* Connected */}
      {connected.length > 0 && (
        <div>
          <h2 className="text-lg font-semibold text-white mb-4 flex items-center gap-2">
            <div className="w-2 h-2 rounded-full bg-green-400" />
            {t('connectedServices')}
          </h2>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {connected.map((integration) => (
              <IntegrationCard
                key={integration.provider}
                integration={integration}
                onConnect={() => handleConnect(integration.provider)}
                onDisconnect={() => handleDisconnect(integration.provider)}
              />
            ))}
          </div>
        </div>
      )}

      {/* Available */}
      {available.length > 0 && (
        <div>
          <h2 className="text-lg font-semibold text-white mb-4">{t('availableIntegrations')}</h2>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {available.map((integration) => (
              <IntegrationCard
                key={integration.provider}
                integration={integration}
                onConnect={() => handleConnect(integration.provider)}
                onDisconnect={() => handleDisconnect(integration.provider)}
              />
            ))}
          </div>
        </div>
      )}

      {/* Empty state */}
      {integrations && integrations.length === 0 && (
        <div className="bg-[#16213e] rounded-2xl p-12 border border-slate-700/50 text-center">
          <svg className="w-16 h-16 mx-auto text-slate-600 mb-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M13.828 10.172a4 4 0 00-5.656 0l-4 4a4 4 0 105.656 5.656l1.102-1.101m-.758-4.899a4 4 0 005.656 0l4-4a4 4 0 00-5.656-5.656l-1.1 1.1" />
          </svg>
          <p className="text-slate-400 text-lg">{t('emptyState')}</p>
          <p className="text-slate-500 text-sm mt-1">
            {t('emptyStateHint')}
          </p>
        </div>
      )}
    </div>
  );
}
