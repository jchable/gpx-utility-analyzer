interface CardProps {
  children: React.ReactNode;
  title?: string;
  id?: string;
  variant?: 'default' | 'inner' | 'interactive';
  className?: string;
  onClick?: () => void;
}

const variantClasses = {
  default: 'bg-surface-card rounded-2xl border border-border p-6',
  inner: 'bg-surface-alt/50 rounded-xl p-4',
  interactive: 'bg-surface-card rounded-2xl border border-border p-6 hover:border-content-muted/30 cursor-pointer transition-colors',
} as const;

export default function Card({
  children,
  title,
  id,
  variant = 'default',
  className = '',
  onClick,
}: CardProps) {
  return (
    <div
      id={id}
      className={`${variantClasses[variant]} ${className}`}
      onClick={onClick}
    >
      {title && (
        <h2 className="text-lg font-semibold text-content mb-4">{title}</h2>
      )}
      {children}
    </div>
  );
}
