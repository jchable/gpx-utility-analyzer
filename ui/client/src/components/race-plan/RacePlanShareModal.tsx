import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Copy, Check, Share2, Trash2 } from 'lucide-react';
import Modal from '../ui/Modal';
import { useEnableShare, useDisableShare } from '../../hooks/useRacePlans';
import type { RacePlanDetail } from '../../types/race-plan';

interface Props {
  plan: RacePlanDetail;
  onClose: () => void;
}

export default function RacePlanShareModal({ plan, onClose }: Props) {
  const { t } = useTranslation('race-plans');
  const enableShare = useEnableShare();
  const disableShare = useDisableShare();
  const [copied, setCopied] = useState(false);

  const shareUrl = plan.shareToken
    ? `${window.location.origin}/share/race-plan/${plan.shareToken}`
    : null;

  async function handleEnable() {
    await enableShare.mutateAsync(plan.id);
  }

  async function handleDisable() {
    if (!confirm(t('share.disable') + '?')) return;
    await disableShare.mutateAsync(plan.id);
  }

  async function handleCopy() {
    if (!shareUrl) return;
    await navigator.clipboard.writeText(shareUrl);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }

  const isPending = enableShare.isPending || disableShare.isPending;

  return (
    <Modal title={t('share.title')} onClose={onClose} maxWidth="max-w-md">
      <div className="space-y-4">
        <p className="text-sm text-content-muted">{t('share.description')}</p>

        {!plan.isPublic ? (
          <button
            onClick={handleEnable}
            disabled={isPending}
            className="w-full flex items-center justify-center gap-2 py-2.5 bg-cyan-600 hover:bg-cyan-500 disabled:opacity-50 text-white rounded-lg text-sm font-medium transition-colors"
          >
            <Share2 size={16} />
            {isPending ? '…' : t('share.enable')}
          </button>
        ) : (
          <div className="space-y-3">
            {/* Share URL */}
            <div>
              <label className="block text-xs font-medium text-content-muted mb-1.5">
                {t('share.linkLabel')}
              </label>
              <div className="flex items-center gap-2">
                <input
                  type="text"
                  readOnly
                  value={shareUrl ?? ''}
                  className="flex-1 bg-surface-alt border border-border rounded-lg px-3 py-2 text-xs text-content-muted font-mono focus:outline-none"
                />
                <button
                  onClick={handleCopy}
                  className={`flex items-center gap-1.5 px-3 py-2 rounded-lg text-sm font-medium transition-colors ${
                    copied
                      ? 'bg-green-600 text-white'
                      : 'bg-surface-alt border border-border text-content-muted hover:text-content'
                  }`}
                >
                  {copied ? <Check size={15} /> : <Copy size={15} />}
                  {copied ? t('share.copied') : t('share.copyLink')}
                </button>
              </div>
            </div>

            {/* Disable sharing */}
            <button
              onClick={handleDisable}
              disabled={isPending}
              className="flex items-center gap-2 text-sm text-red-400 hover:text-red-300 disabled:opacity-50 transition-colors"
            >
              <Trash2 size={14} />
              {t('share.disable')}
            </button>
          </div>
        )}

        <div className="flex justify-end pt-2">
          <button
            onClick={onClose}
            className="px-4 py-2 text-sm text-content-muted hover:text-content transition-colors"
          >
            Close
          </button>
        </div>
      </div>
    </Modal>
  );
}
