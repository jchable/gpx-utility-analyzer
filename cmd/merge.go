package cmd

import (
	"fmt"
	"os"

	"github.com/jchable/gpx-utility-analyzer/internal/gpx"
	"github.com/jchable/gpx-utility-analyzer/internal/input"
	"github.com/jchable/gpx-utility-analyzer/internal/merge"
	"github.com/jchable/gpx-utility-analyzer/internal/output"
	"github.com/jchable/gpx-utility-analyzer/internal/stats"
	"github.com/spf13/cobra"
)

var (
	mergeOutput  string
	mergeSort    bool
	mergeAnalyze bool
)

var mergeCmd = &cobra.Command{
	Use:   "merge [files...]",
	Short: "Merge multiple GPX files into one",
	Long: `Merge multiple GPX files, directories, or glob patterns into a single GPX file.
Points can be sorted by time and statistics can be computed on the result.`,
	Args: cobra.MinimumNArgs(1),
	RunE: runMerge,
}

func init() {
	mergeCmd.Flags().StringVarP(&mergeOutput, "output", "o", "merged.gpx",
		"Output file path")
	mergeCmd.Flags().BoolVar(&mergeSort, "sort", true,
		"Sort track points by time")
	mergeCmd.Flags().BoolVar(&mergeAnalyze, "analyze", false,
		"Print statistics for the merged result")

	mergeCmd.Flags().StringVar(&presetFlag, "preset", stats.DefaultPreset(),
		"Stop detection preset: hiking, trail, or cycling")
	mergeCmd.Flags().Float64Var(&elevThreshold, "elevation-threshold", 2.0,
		"Min elevation change to count (meters, noise filter)")
	mergeCmd.Flags().StringVar(&smoothingFlag, "smoothing", "medium",
		"Elevation smoothing: none, light, medium, heavy")
	mergeCmd.Flags().StringVar(&demDirFlag, "dem-dir", "",
		"Directory containing SRTM .hgt files for DEM elevation correction")

	rootCmd.AddCommand(mergeCmd)
}

func runMerge(cmd *cobra.Command, args []string) error {
	files, err := input.ResolveFiles(args)
	if err != nil {
		return err
	}

	fmt.Fprintf(os.Stdout, "Merging %d files...\n", len(files))

	var docs []*gpx.GPX
	for _, f := range files {
		g, err := gpx.ParseFile(f)
		if err != nil {
			fmt.Fprintf(os.Stderr, "Warning: skipping %s: %v\n", f, err)
			continue
		}
		docs = append(docs, g)
		fmt.Fprintf(os.Stdout, "  + %s (%d points)\n", f, g.PointCount())
	}

	merged, err := merge.Merge(docs, mergeSort)
	if err != nil {
		return err
	}

	if err := gpx.WriteFile(merged, mergeOutput); err != nil {
		return fmt.Errorf("writing merged file: %w", err)
	}

	fmt.Fprintf(os.Stdout, "\nMerged output: %s (%d points)\n", mergeOutput, merged.PointCount())

	if mergeAnalyze {
		points, err := merged.AllPoints()
		if err != nil {
			return err
		}
		cfg := buildComputeConfig()
		summary := stats.Compute(points, merged.SegmentCount(), cfg)

		formatter, err := output.NewFormatter(formatFlag)
		if err != nil {
			return err
		}
		return formatter.Format(os.Stdout, mergeOutput, summary, cfg.StopConfig)
	}

	return nil
}
