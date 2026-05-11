import { useRef, useState, useCallback } from 'react';
import { Upload } from 'lucide-react';

interface GpxDropZoneProps {
  onFiles: (files: File[]) => void;
  accept?: string;
  accentColor?: string;
  label: string;
  hint: string;
  multiple?: boolean;
}

export default function GpxDropZone({
  onFiles,
  accept = '.gpx',
  accentColor = 'cyan',
  label,
  hint,
  multiple = true,
}: GpxDropZoneProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [dragOver, setDragOver] = useState(false);

  const handleDrop = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault();
      setDragOver(false);
      const files = Array.from(e.dataTransfer.files).filter((f) =>
        f.name.toLowerCase().endsWith('.gpx')
      );
      if (files.length > 0) onFiles(files);
    },
    [onFiles]
  );

  const handleFileChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const files = e.target.files ? Array.from(e.target.files) : [];
      if (files.length > 0) onFiles(files);
      // Reset so the same file can be selected again
      e.target.value = '';
    },
    [onFiles]
  );

  const activeClass = dragOver
    ? `border-${accentColor}-400 bg-${accentColor}-400/5`
    : 'border-border hover:border-content-muted/50';

  return (
    <div
      className={`border-2 border-dashed rounded-xl p-8 text-center cursor-pointer transition-colors ${activeClass}`}
      onClick={() => inputRef.current?.click()}
      onDragOver={(e) => {
        e.preventDefault();
        setDragOver(true);
      }}
      onDragLeave={() => setDragOver(false)}
      onDrop={handleDrop}
    >
      <input
        ref={inputRef}
        type="file"
        accept={accept}
        multiple={multiple}
        className="hidden"
        onChange={handleFileChange}
      />
      <Upload size={32} className="mx-auto mb-3 text-content-muted" />
      <p className="text-content font-medium">{label}</p>
      <p className="text-sm text-content-muted mt-1">{hint}</p>
    </div>
  );
}
