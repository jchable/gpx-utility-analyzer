interface TextInputProps {
  value: string;
  onChange: (v: string) => void;
  placeholder?: string;
  type?: 'text' | 'password' | 'number';
  className?: string;
}

export default function TextInput({
  value,
  onChange,
  placeholder,
  type = 'text',
  className = '',
}: TextInputProps) {
  return (
    <input
      type={type}
      value={value}
      onChange={(e) => onChange(e.target.value)}
      placeholder={placeholder}
      className={`w-full bg-surface-input border border-border rounded-lg px-3 py-2 text-content text-sm focus:outline-none focus:border-blue-500/50 placeholder-content-muted/60 ${className}`}
    />
  );
}
