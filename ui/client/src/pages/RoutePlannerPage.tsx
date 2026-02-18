import { useState, useRef, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '../api/client';
import type { PredictResult } from '../types/activity';
import TrackMap from '../components/map/TrackMap';
import ElevationProfileChart from '../components/activity/ElevationProfileChart';
import EffortComparisonSection from '../components/activity/EffortComparisonSection';

export default function RoutePlannerPage() {
  const { t } = useTranslation('activities');
  const { t: tc } = useTranslation();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [isDragOver, setIsDragOver] = useState(false);
  const [isAnalyzing, setIsAnalyzing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<PredictResult | null>(null);
  const [fileName, setFileName] = useState<string>('');

  const handleFile = useCallback(async (file: File) => {
    if (!file.name.toLowerCase().endsWith('.gpx')) {
      setError(tc('apiError.INVALID_FILE_TYPE'));
      return;
    }

    setIsAnalyzing(true);
    setError(null);
    setResult(null);
    setFileName(file.name);

    try {
      const prediction = await api.predictRoute(file);
      setResult(prediction);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setIsAnalyzing(false);
    }
  }, [tc]);

  const handleDrop = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault();
      setIsDragOver(false);
      const files = e.dataTransfer.files;
      if (files.length > 0) handleFile(files[0]);
    },
    [handleFile]
  );

  const stats = result?.stats;

  return (
    <div className="max-w-5xl mx-auto space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-white">{t('predict.title')}</h1>
        <p className="text-sm text-slate-400 mt-1">{t('predict.description')}</p>
      </div>

      {/* Upload zone */}
      <div
        onDrop={handleDrop}
        onDragOver={(e) => { e.preventDefault(); setIsDragOver(true); }}
        onDragLeave={(e) => { e.preventDefault(); setIsDragOver(false); }}
        onClick={() => fileInputRef.current?.click()}
        className={`relative border-2 border-dashed rounded-2xl p-6 sm:p-10 text-center cursor-pointer transition-all ${
          isDragOver
            ? 'border-emerald-400 bg-emerald-400/5'
            : 'border-slate-700 hover:border-slate-500 bg-[#16213e]/50'
        }`}
      >
        <input
          ref={fileInputRef}
          type="file"
          accept=".gpx"
          className="hidden"
          onChange={(e) => {
            if (e.target.files?.[0]) handleFile(e.target.files[0]);
            e.target.value = '';
          }}
        />
        <svg
          className={`w-12 h-12 mx-auto mb-3 transition-colors ${isDragOver ? 'text-emerald-400' : 'text-slate-600'}`}
          fill="none" stroke="currentColor" viewBox="0 0 24 24"
        >
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M9 20l-5.447-2.724A1 1 0 013 16.382V5.618a1 1 0 011.447-.894L9 7m0 13l6-3m-6 3V7m6 10l5.447 2.724A1 1 0 0021 18.382V7.618a1 1 0 00-.553-.894L15 4m0 13V4m0 0L9 7" />
        </svg>
        <p className={`text-lg font-medium mb-1 ${isDragOver ? 'text-emerald-400' : 'text-slate-300'}`}>
          {isDragOver ? t('predict.dropActive') : t('predict.dropZone')}
        </p>
        <p className="text-sm text-slate-500">{t('predict.hint')}</p>
      </div>

      {/* Loading */}
      {isAnalyzing && (
        <div className="bg-[#16213e] rounded-2xl p-8 border border-slate-700/50 text-center">
          <div className="animate-spin rounded-full h-10 w-10 border-t-2 border-b-2 border-emerald-400 mx-auto mb-4" />
          <p className="text-slate-300 font-medium">{t('predict.analyzing')}</p>
          <p className="text-slate-500 text-sm mt-1">{fileName}</p>
        </div>
      )}

      {/* Error */}
      {error && (
        <div className="bg-red-900/20 border border-red-800 rounded-xl p-4 text-red-400 text-sm">
          {error}
        </div>
      )}

      {/* Results */}
      {result && stats && (
        <div className="space-y-6">
          <h2 className="text-xl font-semibold text-white">{t('predict.results')}</h2>

          {/* Track Map */}
          {result.track && (
            <div className="h-[300px] sm:h-[400px] rounded-2xl overflow-hidden">
              <TrackMap coordinates={result.track.coordinates} />
            </div>
          )}

          {/* Elevation Profile */}
          {result.profile && result.profile.length > 0 && (
            <ElevationProfileChart
              data={result.profile}
              hasTimestamps={false}
            />
          )}

          {/* Basic stats grid */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            <div className="bg-[#16213e] rounded-xl p-4 border border-slate-700/50">
              <p className="text-xs text-slate-500 mb-1">{t('distance')}</p>
              <p className="text-lg font-bold text-cyan-400">{stats.total_distance_km.toFixed(1)} {tc('unit.km')}</p>
            </div>
            <div className="bg-[#16213e] rounded-xl p-4 border border-slate-700/50">
              <p className="text-xs text-slate-500 mb-1">{t('detail.elevationGain')}</p>
              <p className="text-lg font-bold text-green-400">+{Math.round(stats.elevation_gain_m)} {tc('unit.m')}</p>
            </div>
            <div className="bg-[#16213e] rounded-xl p-4 border border-slate-700/50">
              <p className="text-xs text-slate-500 mb-1">{t('detail.elevationLoss')}</p>
              <p className="text-lg font-bold text-red-400">&minus;{Math.round(stats.elevation_loss_m)} {tc('unit.m')}</p>
            </div>
            <div className="bg-[#16213e] rounded-xl p-4 border border-slate-700/50">
              <p className="text-xs text-slate-500 mb-1">{t('detail.maxElevation')}</p>
              <p className="text-lg font-bold text-white">{Math.round(stats.max_elevation_m)} {tc('unit.m')}</p>
            </div>
          </div>

          {/* Effort Comparison */}
          {stats.effort && <EffortComparisonSection effort={stats.effort} />}
        </div>
      )}
    </div>
  );
}
