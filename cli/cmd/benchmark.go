package cmd

import (
	"fmt"
	"os"
	"path/filepath"
	"time"

	"github.com/jchable/gpx-utility-analyzer/cli/internal/benchmark"
	"github.com/jchable/gpx-utility-analyzer/cli/internal/dem"
	"github.com/jchable/gpx-utility-analyzer/cli/internal/gpx"
	"github.com/jchable/gpx-utility-analyzer/cli/internal/stats"
	"github.com/spf13/cobra"
)

var (
	benchOutputFlag  string
	benchFullFlag    bool
	benchVaryFlag    string
	benchVerboseFlag bool
	benchSortFlag    string
	benchMaxHRFlag   int
)

var benchmarkCmd = &cobra.Command{
	Use:   "benchmark <file.gpx>",
	Short: "Run a GPX analysis across multiple configurations",
	Long: `Benchmark runs the analysis pipeline with many different configuration
combinations (presets, elevation algorithms, smoothing levels, DEM on/off,
algorithm parameters) and produces a comparison table.

By default, a reduced matrix is used (one axis varied at a time, ~25 runs).
Use --full for the complete Cartesian product (~1248 runs).
Use --vary to select specific axes to vary.

Axes: preset, elev-algo, elev-smoothing, track-smoothing, dem, elev-params`,
	Args: cobra.ExactArgs(1),
	RunE: runBenchmark,
}

func init() {
	benchmarkCmd.Flags().StringVarP(&benchOutputFlag, "output", "o", "",
		"CSV output file path")
	benchmarkCmd.Flags().BoolVar(&benchFullFlag, "full", false,
		"Run full Cartesian product of all configurations")
	benchmarkCmd.Flags().StringVar(&benchVaryFlag, "vary", "",
		"Axes to vary (comma-separated: preset,elev-algo,elev-smoothing,track-smoothing,dem,elev-params)")
	benchmarkCmd.Flags().BoolVarP(&benchVerboseFlag, "verbose", "v", false,
		"Print progress to stderr")
	benchmarkCmd.Flags().StringVar(&benchSortFlag, "sort", "",
		"Sort results by column ("+benchmark.SortColumnNames()+")")
	benchmarkCmd.Flags().IntVar(&benchMaxHRFlag, "max-hr", 0,
		"Maximum heart rate (bpm) for HR zone calculation")

	// DEM flags (same as analyze)
	benchmarkCmd.Flags().StringVar(&demDirFlag, "dem-dir", "",
		"Directory containing SRTM .hgt files for DEM elevation correction")
	benchmarkCmd.Flags().StringVar(&demCacheFlag, "dem-cache", "",
		"Cache directory for auto-downloaded SRTM tiles (default: OS cache dir)")
	benchmarkCmd.Flags().BoolVar(&demAutoDownload, "dem-auto-download", true,
		"Auto-download missing SRTM tiles from the internet")
	benchmarkCmd.Flags().IntVar(&demMaxMemoryFlag, "dem-max-memory", 0,
		"Maximum memory (MB) for loaded DEM tiles (0 = no limit)")
	benchmarkCmd.Flags().BoolVar(&demSkipValidation, "dem-skip-validation", false,
		"Skip post-download DEM tile validation (faster)")

	rootCmd.AddCommand(benchmarkCmd)
}

func runBenchmark(cmd *cobra.Command, args []string) error {
	filePath := args[0]

	// Parse GPX file
	g, err := gpx.ParseFile(filePath)
	if err != nil {
		return fmt.Errorf("parsing %s: %w", filePath, err)
	}

	points, err := g.AllPoints()
	if err != nil {
		return fmt.Errorf("extracting points from %s: %w", filePath, err)
	}

	pointCount := len(points)
	segmentCount := g.SegmentCount()

	// Generate combinations
	var combos []benchmark.Combination

	switch {
	case benchFullFlag:
		combos = benchmark.GenerateCombinations(benchmark.FullMatrixConfig())
	case benchVaryFlag != "":
		axes, err := benchmark.ParseAxes(benchVaryFlag)
		if err != nil {
			return err
		}
		combos = benchmark.VaryCombinations(axes)
	default:
		combos = benchmark.ReducedCombinations()
	}

	if len(combos) == 0 {
		fmt.Fprintln(os.Stderr, "No configurations generated.")
		return nil
	}

	// Check if any combo needs DEM
	needsDEM := false
	for _, c := range combos {
		if c.UseDEM {
			needsDEM = true
			break
		}
	}

	// Create and preload DEM source if needed
	var demSrc *dem.Source
	if needsDEM {
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
			// Preload tiles for original points
			if err := demSrc.Preload(points); err != nil {
				return fmt.Errorf("DEM preload: %w", err)
			}
		}
	}

	// Report plan
	fmt.Fprintf(os.Stderr, "Benchmarking %s (%d points, %d segments)\n", filepath.Base(filePath), pointCount, segmentCount)
	fmt.Fprintf(os.Stderr, "Running %d configurations...\n", len(combos))

	// Run benchmark
	var demProvider stats.ElevationProvider
	if demSrc != nil {
		demProvider = demSrc
	}

	startAll := time.Now()
	results, err := benchmark.Run(combos, benchmark.RunConfig{
		Points:       points,
		SegmentCount: segmentCount,
		DEMSource:    demProvider,
		MaxHR:        benchMaxHRFlag,
		Verbose:      benchVerboseFlag,
		Stderr:       os.Stderr,
	})
	if err != nil {
		return err
	}
	totalTime := time.Since(startAll)

	// Sort if requested
	if benchSortFlag != "" {
		if !benchmark.SortColumn[benchSortFlag] {
			return fmt.Errorf("unknown sort column %q (valid: %s)", benchSortFlag, benchmark.SortColumnNames())
		}
		benchmark.SortResults(results, benchSortFlag)
	}

	// Output Markdown table to stdout
	benchmark.WriteMarkdownTable(os.Stdout, results, filepath.Base(filePath), pointCount)

	// Output CSV if requested
	if benchOutputFlag != "" {
		if err := benchmark.WriteCSV(benchOutputFlag, results); err != nil {
			return err
		}
		fmt.Fprintf(os.Stderr, "CSV written to %s\n", benchOutputFlag)
	}

	fmt.Fprintf(os.Stderr, "Total wall time: %.1fs\n", totalTime.Seconds())

	return nil
}
