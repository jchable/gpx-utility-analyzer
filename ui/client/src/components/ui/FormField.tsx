interface FormFieldProps {
  label: React.ReactNode;
  children: React.ReactNode;
}

export default function FormField({ label, children }: FormFieldProps) {
  return (
    <div className="mb-4 last:mb-0">
      <label className="block text-sm font-medium text-content-muted mb-1.5">{label}</label>
      {children}
    </div>
  );
}
