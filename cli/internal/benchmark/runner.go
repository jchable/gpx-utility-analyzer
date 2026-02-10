package benchmark

import (
	"fmt"
	"io"
	"time"

	"github.com/jchable/gpx-utility-analyzer/cli/internal/elevation"
	"github.com/jchable/gpx-utility-analyzer/cli/internal/gpx"
	"github.com/jchable/gpx-utility-analyzer/cli/internal/stats"
)

// RunConfig holds the execution context for the benchmark.
type RunConfig struct {
	Points       []gpx.TrackPoint       // original parsed points (will be copied per run)
	SegmentCount int                    // number of GPX segments
	DEMSource    stats.ElevationProvider // shared, preloaded DEM source (nil if no DEM runs)
	MaxHR        int                    // max heart rate for HR zones (0 = disabled)
	Verbose      bool                   // print progress to stderr
	Stderr       io.Writer              // destination for progress output
}

// Run executes all combinations and returns results.
// It deep-copies points for each run to avoid mutation side effects.
func Run(combos []Combination, cfg RunConfig) ([]RunResult, error) {
	results := make([]RunResult, 0, len(combos))
	total := len(combos)

	for i, combo := range combos {
		// Deep-copy points (shallow copy is sufficient: biometric pointers are read-only)
		pointsCopy := make([]gpx.TrackPoint, len(cfg.Points))
		copy(pointsCopy, cfg.Points)

		computeCfg := buildComputeConfig(combo, cfg.DEMSource, cfg.MaxHR)

		start := time.Now()
		summary, _, err := stats.Compute(pointsCopy, cfg.SegmentCount, computeCfg)
		elapsed := time.Since(start)

		if err != nil {
			return results, fmt.Errorf("run %d/%d (%s): %w", i+1, total, combo.Label(), err)
		}

		result := RunResult{
			Combination:     combo,
			Distance2D:      summary.TotalDistance / 1000,
			Distance3D:      summary.TotalDistance3D / 1000,
			ElevGain:        summary.Elevation.Gain,
			ElevLoss:        summary.Elevation.Loss,
			ElevMax:         summary.Elevation.Max,
			ElevMin:         summary.Elevation.Min,
			MovingTime:      summary.MovingTime,
			StoppedTime:     summary.StoppedTime,
			StopCount:       summary.StopCount,
			AvgSpeed:        summary.Speed.AvgMovingSpeed * 3.6, // m/s → km/h
			MaxSpeed:        summary.Speed.MaxSpeed * 3.6,       // m/s → km/h
			FilteredPoints:  summary.FilteredPoints,
			ComputeDuration: elapsed,
		}
		results = append(results, result)

		if cfg.Verbose && cfg.Stderr != nil {
			fmt.Fprintf(cfg.Stderr, "[%d/%d] %s (%dms)\n",
				i+1, total, combo.Label(), elapsed.Milliseconds())
		}
	}

	return results, nil
}

func buildComputeConfig(combo Combination, demSrc stats.ElevationProvider, maxHR int) stats.ComputeConfig {
	preset, ok := stats.Presets[combo.Preset]
	if !ok {
		preset = stats.Presets[stats.PresetHiking]
	}

	var src stats.ElevationProvider
	if combo.UseDEM {
		src = demSrc
	}

	maxSpeed := stats.DefaultMaxReasonableSpeed
	if v, ok := stats.PresetMaxSpeed[combo.Preset]; ok {
		maxSpeed = v
	}

	return stats.ComputeConfig{
		ElevationThreshold: combo.Threshold,
		StopConfig:         preset,
		SmoothingLevel:     elevation.SmoothingLevel(combo.ElevSmoothing),
		DEMSource:          src,
		ElevationCfg: stats.ElevationConfig{
			Algo:        combo.ElevAlgo,
			Threshold:   combo.Threshold,
			Epsilon:     combo.DPEpsilon,
			MinSegLen:   combo.SegMinLen,
			MaxSlopeDev: combo.SegMaxDev,
		},
		TrackSmoothing:     elevation.TrackSmoothingLevel(combo.TrackSmoothing),
		BiometricsCfg:      stats.BiometricsConfig{MaxHR: maxHR},
		MaxReasonableSpeed: maxSpeed,
	}
}
