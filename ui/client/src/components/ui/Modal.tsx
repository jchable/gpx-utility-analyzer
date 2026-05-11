import { useEffect } from 'react';

interface ModalProps {
  title: string;
  onClose: () => void;
  children: React.ReactNode;
  maxWidth?: string;
}

export default function Modal({
  title,
  onClose,
  children,
  maxWidth = 'max-w-sm',
}: ModalProps) {
  // Close on Escape key
  useEffect(() => {
    const handleKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', handleKey);
    return () => window.removeEventListener('keydown', handleKey);
  }, [onClose]);

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm"
      onClick={(e) => {
        if (e.target === e.currentTarget) onClose();
      }}
    >
      <div className={`bg-surface-card border border-border rounded-xl shadow-2xl p-6 ${maxWidth} w-full mx-4`}>
        <h3 className="text-lg font-semibold text-content mb-4">{title}</h3>
        {children}
      </div>
    </div>
  );
}
