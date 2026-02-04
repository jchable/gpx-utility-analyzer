package cmd

import (
	"fmt"
	"os"
	"time"

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

	return stats.ComputeConfig{
		ElevationThreshold: elevThreshold,
		StopConfig:         preset,
	}
}
