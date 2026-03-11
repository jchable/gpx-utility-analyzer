import { useState } from 'react';
import { ChevronDown, ChevronUp } from 'lucide-react';

interface CollapsibleSectionProps {
  title: string;
  icon?: React.ReactNode;
  badge?: string | number;
  defaultExpanded?: boolean;
  children: React.ReactNode;
  className?: string;
}

export default function CollapsibleSection({
  title,
  icon,
  badge,
  defaultExpanded = true,
  children,
  className = '',
}: CollapsibleSectionProps) {
  const [expanded, setExpanded] = useState(defaultExpanded);

  return (
    <div className={className}>
      <button
        type="button"
        onClick={() => setExpanded((v) => !v)}
        className="w-full flex items-center justify-between gap-2 text-left"
      >
        <div className="flex items-center gap-2">
          {icon}
          <h2 className="text-xl font-semibold text-content">{title}</h2>
          {badge != null && (
            <span className="text-xs bg-surface-alt/80 text-content-muted px-2 py-0.5 rounded-full">
              {badge}
            </span>
          )}
        </div>
        {expanded ? (
          <ChevronUp size={20} className="text-content-muted shrink-0" />
        ) : (
          <ChevronDown size={20} className="text-content-muted shrink-0" />
        )}
      </button>

      {expanded && <div className="mt-4">{children}</div>}
    </div>
  );
}
