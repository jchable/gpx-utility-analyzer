import { useState, useRef, useCallback, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useQueryClient } from '@tanstack/react-query';
import { api } from '../api/client';
import { ACTIVITY_TYPES, ACTIVITY_COLORS } from '../types/activity';

type UploadStatus = 'pending' | 'uploading' | 'processing' | 'done' | 'error';

interface FileEntry {
  /** Stable identity: the upload loop is keyed by this, never by array position. */
  id: string;
  file: File;
  status: UploadStatus;
  activityId?: string;
  error?: string;
}

const POLL_INTERVAL = 2000;

let fallbackIdCounter = 0;

function newEntryId(): string {
  return typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
    ? crypto.randomUUID()
    : `file-${Date.now()}-${fallbackIdCounter++}`;
}

export default function UploadPage() {
  const { t } = useTranslation('upload');
  const { t: tc } = useTranslation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [files, setFiles] = useState<FileEntry[]>([]);
  const [activityType, setActivityType] = useState('trail');
  const [isDragOver, setIsDragOver] = useState(false);
  const [isUploading, setIsUploading] = useState(false);

  // A ref mirroring `files`, so the async upload loop reads the LIVE list
  // rather than the array captured when handleUploadAll was created.
  const filesRef = useRef<FileEntry[]>(files);
  useEffect(() => {
    filesRef.current = files;
  }, [files]);

  useEffect(() => {
    const processingFiles = files.filter((f) => f.status === 'processing' && f.activityId);
    if (processingFiles.length === 0) return;

    const interval = setInterval(async () => {
      for (const entry of processingFiles) {
        try {
          const activity = await api.getActivity(entry.activityId!);
          if (activity.status === 'Completed' || activity.status === 'Failed') {
            setFiles((prev) =>
              prev.map((f) =>
                f.activityId === entry.activityId
                  ? {
                      ...f,
                      status: activity.status === 'Completed' ? 'done' : 'error',
                      error: activity.status === 'Failed' ? activity.errorMessage ?? t('uploadFailed') : undefined,
                    }
                  : f
              )
            );
            queryClient.invalidateQueries({ queryKey: ['activities'] });
            queryClient.invalidateQueries({ queryKey: ['dashboard'] });
          }
        } catch {
          // Ignore polling errors
        }
      }
    }, POLL_INTERVAL);

    return () => clearInterval(interval);
  }, [files, queryClient, t]);

  const addFiles = useCallback((newFiles: FileList | File[]) => {
    const gpxFiles = Array.from(newFiles).filter((f) =>
      f.name.toLowerCase().endsWith('.gpx')
    );
    if (gpxFiles.length === 0) return;
    setFiles((prev) => [
      ...prev,
      ...gpxFiles.map((file) => ({ id: newEntryId(), file, status: 'pending' as UploadStatus })),
    ]);
  }, []);

  const removeFile = (id: string) => {
    setFiles((prev) => prev.filter((f) => f.id !== id));
  };

  const handleDrop = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault();
      setIsDragOver(false);
      addFiles(e.dataTransfer.files);
    },
    [addFiles]
  );

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragOver(true);
  };

  const handleDragLeave = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragOver(false);
  };

  const handleUploadAll = async () => {
    if (isUploading) return;

    const queue = files.filter((f) => f.status === 'pending').map((f) => f.id);
    if (queue.length === 0) return;

    setIsUploading(true);

    for (const id of queue) {
      // Re-read from the live list: the entry may have been removed since the
      // queue was built, and its position will have shifted regardless.
      const entry = filesRef.current.find((f) => f.id === id);
      if (!entry || entry.status !== 'pending') continue;

      setFiles((prev) => prev.map((f) => (f.id === id ? { ...f, status: 'uploading' } : f)));

      try {
        const result = await api.uploadGpx(entry.file, activityType);
        setFiles((prev) =>
          prev.map((f) => (f.id === id ? { ...f, status: 'processing', activityId: result.id } : f))
        );
      } catch (err) {
        setFiles((prev) =>
          prev.map((f) =>
            f.id === id
              ? { ...f, status: 'error', error: err instanceof Error ? err.message : t('uploadFailed') }
              : f
          )
        );
      }
    }

    setIsUploading(false);
    queryClient.invalidateQueries({ queryKey: ['activities'] });
    queryClient.invalidateQueries({ queryKey: ['dashboard'] });
  };

  const pendingCount = files.filter((f) => f.status === 'pending').length;
  const processedFiles = files.filter((f) => f.activityId);
  const hasErrors = files.some((f) => f.status === 'error');

  const statusIcon = (status: UploadStatus) => {
    switch (status) {
      case 'pending':
        return <div className="w-5 h-5 rounded-full border-2 border-content-muted/70" />;
      case 'uploading':
        return <div className="w-5 h-5 rounded-full border-2 border-cyan-400 border-t-transparent animate-spin" />;
      case 'processing':
        return <div className="w-5 h-5 rounded-full border-2 border-purple-400 border-t-transparent animate-spin" />;
      case 'done':
        return (
          <svg className="w-5 h-5 text-green-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
          </svg>
        );
      case 'error':
        return (
          <svg className="w-5 h-5 text-red-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
          </svg>
        );
    }
  };

  const statusLabel = (status: UploadStatus) => {
    switch (status) {
      case 'pending': return t('statusReady');
      case 'uploading': return t('statusUploading');
      case 'processing': return t('statusProcessing');
      case 'done': return t('statusComplete');
      case 'error': return t('statusFailed');
    }
  };

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-content">{t('title')}</h1>

      <div>
        <label className="block text-sm font-medium text-content-muted mb-3">{t('activityType')}</label>
        <div className="flex flex-wrap gap-2">
          {ACTIVITY_TYPES.map((key) => {
            const color = ACTIVITY_COLORS[key] || '#888';
            const isSelected = activityType === key;
            return (
              <button
                key={key}
                onClick={() => setActivityType(key)}
                disabled={isUploading}
                className={`px-4 py-2 rounded-lg text-sm font-medium transition-all border ${
                  isSelected
                    ? 'border-transparent text-white'
                    : 'border-border text-content-muted hover:text-content hover:border-content-muted/30 bg-surface-card'
                } disabled:opacity-50`}
                style={isSelected ? { backgroundColor: color + '33', color, borderColor: color + '55' } : undefined}
              >
                {tc(`activityType.${key}`)}
              </button>
            );
          })}
        </div>
      </div>

      <div
        onDrop={handleDrop}
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onClick={() => fileInputRef.current?.click()}
        className={`relative border-2 border-dashed rounded-2xl p-6 sm:p-12 text-center cursor-pointer transition-all ${
          isDragOver
            ? 'border-cyan-400 bg-cyan-400/5'
            : 'border-border hover:border-content-muted/30 bg-surface-card/50'
        }`}
      >
        <input
          ref={fileInputRef}
          type="file"
          accept=".gpx"
          multiple
          className="hidden"
          onChange={(e) => {
            if (e.target.files) addFiles(e.target.files);
            e.target.value = '';
          }}
        />
        <svg
          className={`w-12 h-12 sm:w-16 sm:h-16 mx-auto mb-4 transition-colors ${isDragOver ? 'text-cyan-400' : 'text-content-muted/70'}`}
          fill="none"
          stroke="currentColor"
          viewBox="0 0 24 24"
        >
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12" />
        </svg>
        <p className={`text-lg font-medium mb-1 ${isDragOver ? 'text-cyan-400' : 'text-content'}`}>
          {isDragOver ? t('dropZoneActive') : t('dropZone')}
        </p>
        <p className="text-sm text-content-muted">{t('browseHint')}</p>
      </div>

      {files.length > 0 && (
        <div className="space-y-4">
          <div className="flex items-center justify-between">
            <h2 className="text-lg font-semibold text-content">
              {t('fileCount', { count: files.length })}
            </h2>
            {pendingCount > 0 && !isUploading && (
              <button
                onClick={() => setFiles([])}
                className="text-sm text-content-muted hover:text-red-400 transition-colors"
              >
                {t('clearAll')}
              </button>
            )}
          </div>

          <div className="space-y-2">
            {files.map((entry) => (
              <div
                key={entry.id}
                className="flex items-center gap-3 bg-surface-card rounded-xl px-4 py-3 border border-border"
              >
                {statusIcon(entry.status)}
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-medium text-content truncate">{entry.file.name}</p>
                  <p className="text-xs text-content-muted">
                    {(entry.file.size / 1024).toFixed(0)} {tc('unit.kb')}
                    {entry.error && <span className="text-red-400 ml-2">{entry.error}</span>}
                  </p>
                </div>
                <span className={`text-xs font-medium shrink-0 ${
                  entry.status === 'error' ? 'text-red-400' :
                  entry.status === 'processing' ? 'text-purple-400' :
                  entry.status === 'uploading' ? 'text-cyan-400' :
                  entry.status === 'done' ? 'text-green-400' : 'text-content-muted'
                }`}>
                  {statusLabel(entry.status)}
                </span>
                {entry.activityId ? (
                  <button
                    onClick={() => navigate(`/activities/${entry.activityId}`)}
                    className="px-3 py-1 rounded-lg bg-cyan-600/20 text-cyan-400 text-xs font-medium hover:bg-cyan-600/30 transition-colors shrink-0"
                  >
                    {tc('button.view')}
                  </button>
                ) : entry.status === 'pending' ? (
                  // Only ever rendered for a still-pending entry, i.e. one that
                  // has not been sent. The upload loop is keyed by entry id and
                  // re-reads the live list, so removing one mid-run is safe.
                  <button
                    onClick={() => removeFile(entry.id)}
                    aria-label={t('removeFile', { name: entry.file.name })}
                    className="p-1 rounded-lg text-content-muted/70 hover:text-red-400 transition-colors shrink-0"
                  >
                    <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                    </svg>
                  </button>
                ) : null}
              </div>
            ))}
          </div>

          <div className="flex items-center gap-3">
            {pendingCount > 0 && (
              <button
                onClick={handleUploadAll}
                disabled={isUploading}
                className="px-6 py-3 rounded-xl bg-cyan-600 hover:bg-cyan-500 text-white font-semibold transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2"
              >
                <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12" />
                </svg>
                {isUploading ? t('uploading') : t('uploadButton', { count: pendingCount })}
              </button>
            )}
            {processedFiles.length > 0 && !isUploading && (
              <button
                onClick={() => navigate('/activities')}
                className="px-6 py-3 rounded-xl bg-surface-card border border-border text-content font-medium hover:bg-surface-alt/50 transition-colors"
              >
                {t('viewAllActivities')}
              </button>
            )}
            {hasErrors && !isUploading && (
              <button
                onClick={() => {
                  setFiles((prev) =>
                    prev.map((f) => (f.status === 'error' ? { ...f, status: 'pending', error: undefined } : f))
                  );
                }}
                className="px-4 py-3 rounded-xl text-amber-400 text-sm hover:text-amber-300 transition-colors"
              >
                {t('retryFailed')}
              </button>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
