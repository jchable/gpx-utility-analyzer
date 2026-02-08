import { Brain, TrendingUp, AlertTriangle, Flame, Clock, Target } from 'lucide-react';
import type { TrackReport } from '../../types/activity';

interface AiReportPanelProps {
  report: TrackReport;
}

function getDifficultyColor(score: number): string {
  if (score <= 3) return '#00ff88';   // easy - green
  if (score <= 5) return '#00d4ff';   // moderate - cyan
  if (score <= 7) return '#ff8800';   // hard - orange
  return '#ff4444';                   // very hard - red
}

function getDifficultyBg(score: number): string {
  if (score <= 3) return 'rgba(0,255,136,0.12)';
  if (score <= 5) return 'rgba(0,212,255,0.12)';
  if (score <= 7) return 'rgba(255,136,0,0.12)';
  return 'rgba(255,68,68,0.12)';
}

function getSegmentIcon(type: string) {
  const lower = type.toLowerCase();
  if (lower.includes('climb') || lower.includes('ascent') || lower.includes('up')) {
    return <TrendingUp size={14} />;
  }
  if (lower.includes('danger') || lower.includes('difficult') || lower.includes('techni')) {
    return <AlertTriangle size={14} />;
  }
  return <Target size={14} />;
}

export default function AiReportPanel({ report }: AiReportPanelProps) {
  const diffColor = getDifficultyColor(report.difficulty.score);
  const diffBg = getDifficultyBg(report.difficulty.score);

  return (
    <div className="flex flex-col gap-4">
      {/* Header */}
      <div className="flex items-center gap-2">
        <Brain size={20} className="text-[#00d4ff]" />
        <h3 className="text-base font-semibold text-white">AI Analysis</h3>
      </div>

      {/* Summary */}
      <div className="bg-[#16213e] rounded-xl border border-white/5 p-4">
        <p className="text-sm text-[#e0e0e0] leading-relaxed">{report.summary}</p>
      </div>

      {/* Difficulty badge */}
      <div className="flex items-center gap-3">
        <div
          className="inline-flex items-center gap-2 px-3 py-1.5 rounded-full text-sm font-bold border"
          style={{
            color: diffColor,
            backgroundColor: diffBg,
            borderColor: `${diffColor}30`,
          }}
        >
          <span>{report.difficulty.grade}</span>
          <span className="text-xs font-normal opacity-80">
            {report.difficulty.score}/10
          </span>
        </div>
        <span className="text-xs text-[#a0a0b0] flex-1">
          {report.difficulty.justification}
        </span>
      </div>

      {/* Key Segments */}
      {report.key_segments && report.key_segments.length > 0 && (
        <div className="bg-[#16213e] rounded-xl border border-white/5 p-4">
          <h4 className="text-sm font-semibold text-[#a0a0b0] mb-3 tracking-wide uppercase">
            Key Segments
          </h4>
          <ul className="flex flex-col gap-2">
            {report.key_segments.map((seg, idx) => (
              <li
                key={idx}
                className="flex items-start gap-2 text-sm text-[#e0e0e0]"
              >
                <span className="text-[#00d4ff] mt-0.5 shrink-0">
                  {getSegmentIcon(seg.type)}
                </span>
                <div className="flex flex-col">
                  <span className="font-medium text-white">{seg.type}</span>
                  <span className="text-[#a0a0b0] text-xs">
                    {seg.description}
                    {seg.distance_km != null && (
                      <> &middot; {seg.distance_km.toFixed(1)} km</>
                    )}
                    {seg.elevation_change != null && (
                      <> &middot; {seg.elevation_change > 0 ? '+' : ''}
                        {Math.round(seg.elevation_change)} m</>
                    )}
                  </span>
                </div>
              </li>
            ))}
          </ul>
        </div>
      )}

      {/* Recommendations */}
      {report.recommendations && report.recommendations.length > 0 && (
        <div className="bg-[#16213e] rounded-xl border border-white/5 p-4">
          <h4 className="text-sm font-semibold text-[#a0a0b0] mb-3 tracking-wide uppercase">
            Recommendations
          </h4>
          <ul className="flex flex-col gap-2">
            {report.recommendations.map((rec, idx) => (
              <li
                key={idx}
                className="flex items-start gap-2 text-sm text-[#e0e0e0]"
              >
                <span className="text-[#00ff88] mt-0.5 shrink-0">
                  <Target size={14} />
                </span>
                <span>{rec}</span>
              </li>
            ))}
          </ul>
        </div>
      )}

      {/* Effort Estimate */}
      {report.effort && (
        <div className="bg-[#16213e] rounded-xl border border-white/5 p-4">
          <h4 className="text-sm font-semibold text-[#a0a0b0] mb-3 tracking-wide uppercase">
            Effort Estimate
          </h4>
          <div className="flex flex-wrap gap-4">
            <div className="flex items-center gap-2 text-sm">
              <Target size={14} className="text-[#00d4ff]" />
              <span className="text-[#a0a0b0]">Fitness Level:</span>
              <span className="text-white font-medium">
                {report.effort.fitness_level}
              </span>
            </div>
            <div className="flex items-center gap-2 text-sm">
              <Clock size={14} className="text-[#ff8800]" />
              <span className="text-[#a0a0b0]">Duration:</span>
              <span className="text-white font-medium">
                {report.effort.estimated_duration}
              </span>
            </div>
            {report.effort.calorie_estimate != null && (
              <div className="flex items-center gap-2 text-sm">
                <Flame size={14} className="text-[#ff4444]" />
                <span className="text-[#a0a0b0]">Calories:</span>
                <span className="text-white font-medium">
                  ~{report.effort.calorie_estimate} kcal
                </span>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
