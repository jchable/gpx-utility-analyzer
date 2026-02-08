package cmd

import (
	"fmt"
	"os"
	"path/filepath"
	"time"

	"github.com/jchable/gpx-utility-analyzer/cli/internal/gpx"
	"github.com/jchable/gpx-utility-analyzer/cli/internal/output"
	"github.com/jchable/gpx-utility-analyzer/cli/internal/split"
	"github.com/jchable/gpx-utility-analyzer/cli/internal/stats"
	"github.com/spf13/cobra"
)

var (
	splitInterval  time.Duration
	splitOutputDir string
	splitPrefix    string
)

var splitCmd = &cobra.Command{
	Use:   "split <file>",
	Short: "Split a GPX file by time intervals",
	Long: `Split a GPX file into multiple files based on time intervals.
Each segment gets its own GPX file and statistics are displayed.`,
	Args: cobra.ExactArgs(1),
	RunE: runSplit,
}

func init() {
	splitCmd.Flags().DurationVar(&splitInterval, "interval", 24*time.Hour,
		"Split interval (e.g. 24h, 12h, 30m)")
	splitCmd.Flags().StringVar(&splitOutputDir, "output-dir", "splits",
		"Directory for split GPX files")
	splitCmd.Flags().StringVar(&splitPrefix, "prefix", "segment",
		"Filename prefix for split files")

	// Reuse stop detection flags
	splitCmd.Flags().StringVar(&presetFlag, "preset", stats.DefaultPreset(),
		"Stop detection preset: hiking, trail, or cycling")
	splitCmd.Flags().Float64Var(&stopSpeedFlag, "stop-speed", 0,
		"Override max speed for stop detection (m/s)")
	splitCmd.Flags().DurationVar(&stopDurationFlag, "stop-duration", 0,
		"Override min duration for stop detection (e.g. 2m)")
	splitCmd.Flags().Float64Var(&elevThreshold, "elevation-threshold", 2.0,
		"Min elevation change to count (meters, noise filter)")
	splitCmd.Flags().StringVar(&smoothingFlag, "smoothing", "medium",
		"Elevation smoothing: none, light, medium, heavy")
	splitCmd.Flags().StringVar(&demDirFlag, "dem-dir", "",
		"Directory containing SRTM .hgt files for DEM elevation correction")
	splitCmd.Flags().StringVar(&demCacheFlag, "dem-cache", "",
		"Cache directory for auto-downloaded SRTM tiles (default: OS cache dir)")
	splitCmd.Flags().BoolVar(&demAutoDownload, "dem-auto-download", true,
		"Auto-download missing SRTM tiles from the internet")
	splitCmd.Flags().StringVar(&elevAlgoFlag, "elevation-algo", "threshold",
		"Elevation algorithm: threshold, douglas-peucker, or segments")
	splitCmd.Flags().StringVar(&trackSmoothFlag, "track-smoothing", "none",
		"GPS track lat/lon smoothing: none, light, medium, heavy")
	splitCmd.Flags().Float64Var(&dpEpsilonFlag, "dp-epsilon", 3.0,
		"Douglas-Peucker epsilon: max vertical deviation in meters")
	splitCmd.Flags().Float64Var(&segMinLenFlag, "seg-min-length", 200.0,
		"Segments algo: minimum segment length in meters")
	splitCmd.Flags().Float64Var(&segMaxDevFlag, "seg-max-deviation", 2.0,
		"Segments algo: max RMS residual in meters")

	rootCmd.AddCommand(splitCmd)
}

func runSplit(cmd *cobra.Command, args []string) error {
	path := args[0]

	g, err := gpx.ParseFile(path)
	if err != nil {
		return fmt.Errorf("parsing %s: %w", path, err)
	}

	points, err := g.AllPoints()
	if err != nil {
		return fmt.Errorf("extracting points from %s: %w", path, err)
	}

	segments, err := split.ByTime(points, splitInterval)
	if err != nil {
		return fmt.Errorf("splitting %s: %w", path, err)
	}

	formatter, err := output.NewFormatter(formatFlag)
	if err != nil {
		return err
	}

	cfg := buildComputeConfig()

	fmt.Fprintf(os.Stdout, "Split %s into %d segments (interval: %s)\n\n",
		path, len(segments), splitInterval)

	for _, seg := range segments {
		// Write GPX file
		segName := fmt.Sprintf("%s-%03d", splitPrefix, seg.Index+1)
		outPath := filepath.Join(splitOutputDir, segName+".gpx")

		segGPX := gpx.NewGPXFromPoints(seg.Points, segName)
		if err := gpx.WriteFile(segGPX, outPath); err != nil {
			fmt.Fprintf(os.Stderr, "Error writing %s: %v\n", outPath, err)
			continue
		}

		// Compute and display stats
		segPoints := make([]gpx.TrackPoint, len(seg.Points))
		copy(segPoints, seg.Points)
		summary := stats.Compute(segPoints, 1, cfg)

		fmt.Fprintf(os.Stdout, "--- Segment %d: %s → %s ---\n",
			seg.Index+1,
			seg.StartTime.Format("2006-01-02 15:04"),
			seg.EndTime.Format("2006-01-02 15:04"))
		fmt.Fprintf(os.Stdout, "File: %s\n", outPath)

		if err := formatter.Format(os.Stdout, segName, summary, cfg.StopConfig); err != nil {
			fmt.Fprintf(os.Stderr, "Error formatting segment %d: %v\n", seg.Index+1, err)
		}
		fmt.Fprintln(os.Stdout)
	}

	return nil
}
