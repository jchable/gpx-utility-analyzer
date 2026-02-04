package cmd

import (
	"fmt"
	"os"
	"time"

	"github.com/jchable/gpx-utility-analyzer/internal/dem"
	"github.com/jchable/gpx-utility-analyzer/internal/elevation"
	"github.com/jchable/gpx-utility-analyzer/internal/gpx"
	"github.com/jchable/gpx-utility-analyzer/internal/input"
	"github.com/jchable/gpx-utility-analyzer/internal/output"
	"github.com/jchable/gpx-utility-analyzer/internal/stats"
	"github.com/spf13/cobra"
)

var (
	presetFlag       string
	stopSpeedFlag    float64
	stopDurationFlag time.Duration
	elevThreshold    float64
	smoothingFlag    string
	demDirFlag       string
	demCacheFlag     string
	demAutoDownload  bool
)

var analyzeCmd = &cobra.Command{
	Use:   "analyze [files...]",
	Short: "Analyze one or more GPX files",
	Long:  `Compute statistics for GPX files: distance, elevation, speed, pace, stops.`,
	Args:  cobra.MinimumNArgs(1),
	RunE:  runAnalyze,
}

func init() {
	analyzeCmd.Flags().StringVar(&presetFlag, "preset", stats.DefaultPreset(),
		"Stop detection preset: hiking, trail, or cycling")
	analyzeCmd.Flags().Float64Var(&stopSpeedFlag, "stop-speed", 0,
		"Override max speed for stop detection (m/s)")
	analyzeCmd.Flags().DurationVar(&stopDurationFlag, "stop-duration", 0,
		"Override min duration for stop detection (e.g. 2m)")
	analyzeCmd.Flags().Float64Var(&elevThreshold, "elevation-threshold", 2.0,
		"Min elevation change to count (meters, noise filter)")
	analyzeCmd.Flags().StringVar(&smoothingFlag, "smoothing", "medium",
		"Elevation smoothing: none, light, medium, heavy")
	analyzeCmd.Flags().StringVar(&demDirFlag, "dem-dir", "",
		"Directory containing SRTM .hgt files for DEM elevation correction")
	analyzeCmd.Flags().StringVar(&demCacheFlag, "dem-cache", "",
		"Cache directory for auto-downloaded SRTM tiles (default: OS cache dir)")
	analyzeCmd.Flags().BoolVar(&demAutoDownload, "dem-auto-download", true,
		"Auto-download missing SRTM tiles from the internet")

	rootCmd.AddCommand(analyzeCmd)
}

func runAnalyze(cmd *cobra.Command, args []string) error {
	formatter, err := output.NewFormatter(formatFlag)
	if err != nil {
		return err
	}

	files, err := input.ResolveFiles(args)
	if err != nil {
		return err
	}

	cfg := buildComputeConfig()

	for _, path := range files {
		if err := analyzeFile(path, formatter, cfg); err != nil {
			fmt.Fprintf(os.Stderr, "Error analyzing %s: %v\n", path, err)
		}
	}

	return nil
}

func analyzeFile(path string, formatter output.Formatter, cfg stats.ComputeConfig) error {
	g, err := gpx.ParseFile(path)
	if err != nil {
		return fmt.Errorf("parsing %s: %w", path, err)
	}

	points, err := g.AllPoints()
	if err != nil {
		return fmt.Errorf("extracting points from %s: %w", path, err)
	}

	summary := stats.Compute(points, g.SegmentCount(), cfg)

	return formatter.Format(os.Stdout, path, summary, cfg.StopConfig)
}

func buildComputeConfig() stats.ComputeConfig {
	preset, ok := stats.Presets[presetFlag]
	if !ok {
		fmt.Fprintf(os.Stderr, "Warning: unknown preset %q, using %s\n", presetFlag, stats.DefaultPreset())
		preset = stats.Presets[stats.DefaultPreset()]
	}

	// Apply overrides
	if stopSpeedFlag > 0 {
		preset.MaxSpeed = stopSpeedFlag
	}
	if stopDurationFlag > 0 {
		preset.MinDuration = stopDurationFlag
	}

	// Smoothing
	level := elevation.SmoothingLevel(smoothingFlag)
	if !elevation.ValidLevel(smoothingFlag) {
		fmt.Fprintf(os.Stderr, "Warning: unknown smoothing level %q, using medium\n", smoothingFlag)
		level = elevation.SmoothMedium
	}

	// DEM source
	var demSrc *dem.Source
	cacheDir := demCacheFlag
	if cacheDir == "" {
		cacheDir = dem.DefaultCacheDir()
	}
	switch {
	case demDirFlag != "" && demAutoDownload:
		demSrc = dem.NewSourceWithCache(demDirFlag, cacheDir, true)
	case demDirFlag != "":
		demSrc = dem.NewSourceWithCache(demDirFlag, cacheDir, false)
	case demAutoDownload:
		demSrc = dem.NewAutoSource(cacheDir)
	}

	return stats.ComputeConfig{
		ElevationThreshold: elevThreshold,
		StopConfig:         preset,
		SmoothingLevel:     level,
		DEMSource:          demSrc,
	}
}
