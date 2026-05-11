interface SelectInputProps {
  value: string;
  onChange: (v: string) => void;
  options: { value: string; label: string }[];
  className?: string;
}

export default function SelectInput({
  value,
  onChange,
  options,
  className = '',
}: SelectInputProps) {
  return (
    <select
      value={value}
      onChange={(e) => onChange(e.target.value)}
      className={`w-full bg-surface-input border border-border rounded-lg px-3 py-2 text-content text-sm focus:outline-none focus:border-blue-500/50 ${className}`}
    >
      {options.map((opt) => (
        <option key={opt.value} value={opt.value}>
          {opt.label}
        </option>
      ))}
    </select>
  );
}
