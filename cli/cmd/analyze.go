package cmd

import (
	"fmt"
	"os"
	"path/filepath"
	"strings"
	"time"

	"github.com/jchable/gpx-utility-analyzer/cli/internal/dem"
	"github.com/jchable/gpx-utility-analyzer/cli/internal/elevation"
	"github.com/jchable/gpx-utility-analyzer/cli/internal/gpx"
	"github.com/jchable/gpx-utility-analyzer/cli/internal/input"
	"github.com/jchable/gpx-utility-analyzer/cli/internal/output"
	"github.com/jchable/gpx-utility-analyzer/cli/internal/stats"
	"github.com/spf13/cobra"
)

var (
	presetFlag        string
	stopSpeedFlag     float64
	stopDurationFlag  time.Duration
	elevThreshold     float64
	smoothingFlag     string
	demDirFlag        string
	demCacheFlag      string
	demAutoDownload   bool
	demMaxMemoryFlag  int
	demSkipValidation bool
	elevAlgoFlag      string
	trackSmoothFlag   string
	dpEpsilonFlag     float64
	segMinLenFlag     float64
	segMaxDevFlag     float64
	exportDirFlag     string
	maxHRFlag         int
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
	analyzeCmd.Flags().IntVar(&demMaxMemoryFlag, "dem-max-memory", 0,
		"Maximum memory (MB) for loaded DEM tiles (0 = no limit)")
	analyzeCmd.Flags().BoolVar(&demSkipValidation, "dem-skip-validation", false,
		"Skip post-download DEM tile validation (faster)")
	analyzeCmd.Flags().StringVar(&elevAlgoFlag, "elevation-algo", "threshold",
		"Elevation algorithm: threshold, douglas-peucker, or segments")
	analyzeCmd.Flags().StringVar(&trackSmoothFlag, "track-smoothing", "none",
		"GPS track lat/lon smoothing: none, light, medium, heavy")
	analyzeCmd.Flags().Float64Var(&dpEpsilonFlag, "dp-epsilon", 3.0,
		"Douglas-Peucker epsilon: max vertical deviation in meters")
	analyzeCmd.Flags().Float64Var(&segMinLenFlag, "seg-min-length", 200.0,
		"Segments algo: minimum segment length in meters")
	analyzeCmd.Flags().Float64Var(&segMaxDevFlag, "seg-max-deviation", 2.0,
		"Segments algo: max RMS residual in meters")
	analyzeCmd.Flags().StringVar(&exportDirFlag, "export", "",
		"Export preprocessed GPX files to this directory")
	analyzeCmd.Flags().IntVar(&maxHRFlag, "max-hr", 0,
		"Maximum heart rate (bpm) for HR zone calculation")

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

	// Compute modifies points in place (track smoothing, DEM, elevation smoothing)
	summary, err := stats.Compute(points, g.SegmentCount(), cfg)
	if err != nil {
		return fmt.Errorf("computing stats for %s: %w", path, err)
	}

	if err := formatter.Format(os.Stdout, path, summary, cfg.StopConfig); err != nil {
		return err
	}

	// Export preprocessed GPX if requested
	if exportDirFlag != "" {
		base := filepath.Base(path)
		name := strings.TrimSuffix(base, filepath.Ext(base)) + "_processed.gpx"
		outPath := filepath.Join(exportDirFlag, name)
		exported := gpx.NewGPXFromPoints(points, strings.TrimSuffix(base, filepath.Ext(base)))
		if err := gpx.WriteFile(exported, outPath); err != nil {
			return fmt.Errorf("exporting %s: %w", outPath, err)
		}
		fmt.Fprintf(os.Stdout, "Exported: %s (%d points)\n", outPath, len(points))
	}

	return nil
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
	if demSrc != nil {
		demSrc.WithMaxMemory(demMaxMemoryFlag).WithSkipValidation(demSkipValidation)
	}

	// Elevation algorithm
	elevAlgo := stats.ElevationAlgo(elevAlgoFlag)
	if !stats.ValidAlgo(elevAlgoFlag) {
		fmt.Fprintf(os.Stderr, "Warning: unknown elevation algo %q, using threshold\n", elevAlgoFlag)
		elevAlgo = stats.AlgoThreshold
	}

	// Track smoothing
	trackSmooth := elevation.TrackSmoothingLevel(trackSmoothFlag)
	if !elevation.ValidTrackSmoothingLevel(trackSmoothFlag) {
		fmt.Fprintf(os.Stderr, "Warning: unknown track smoothing %q, using none\n", trackSmoothFlag)
		trackSmooth = elevation.TrackSmoothNone
	}

	return stats.ComputeConfig{
		ElevationThreshold: elevThreshold,
		StopConfig:         preset,
		SmoothingLevel:     level,
		DEMSource:          demSrc,
		ElevationCfg: stats.ElevationConfig{
			Algo:        elevAlgo,
			Threshold:   elevThreshold,
			Epsilon:     dpEpsilonFlag,
			MinSegLen:   segMinLenFlag,
			MaxSlopeDev: segMaxDevFlag,
		},
		TrackSmoothing: trackSmooth,
		BiometricsCfg:  stats.BiometricsConfig{MaxHR: maxHRFlag},
	}
}
