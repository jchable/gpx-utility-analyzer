import { useTranslation } from 'react-i18next';

interface PaginationProps {
  page: number;
  totalPages: number;
  onPageChange: (page: number) => void;
}

export default function Pagination({ page, totalPages, onPageChange }: PaginationProps) {
  const { t } = useTranslation();

  if (totalPages <= 1) return null;

  return (
    <div className="flex items-center justify-between mt-6">
      <button
        onClick={() => onPageChange(page - 1)}
        disabled={page <= 1}
        className="px-4 py-2 rounded-lg bg-surface-card border border-border text-content-muted text-sm disabled:opacity-40 disabled:cursor-not-allowed hover:bg-surface-alt/50 transition-colors"
      >
        {t('button.previous')}
      </button>
      <span className="text-sm text-content-muted">
        {t('format.page', { page, totalPages })}
      </span>
      <button
        onClick={() => onPageChange(page + 1)}
        disabled={page >= totalPages}
        className="px-4 py-2 rounded-lg bg-surface-card border border-border text-content-muted text-sm disabled:opacity-40 disabled:cursor-not-allowed hover:bg-surface-alt/50 transition-colors"
      >
        {t('button.next')}
      </button>
    </div>
  );
}
