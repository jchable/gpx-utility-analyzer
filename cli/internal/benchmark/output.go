package benchmark

import (
	"encoding/csv"
	"fmt"
	"io"
	"os"
	"sort"
	"strings"
	"time"

	"github.com/olekukonko/tablewriter"
)

// WriteMarkdownTable writes results as a formatted table to w.
func WriteMarkdownTable(w io.Writer, results []RunResult, filename string, pointCount int) {
	fmt.Fprintf(w, "\n=== Benchmark: %s (%d points) ===\n", filename, pointCount)
	fmt.Fprintf(w, "Configurations: %d\n\n", len(results))

	if len(results) == 0 {
		fmt.Fprintln(w, "No results.")
		return
	}

	table := tablewriter.NewTable(w)
	table.Header(Headers())
	for i, r := range results {
		table.Append(r.Row(i + 1))
	}
	table.Render()

	// Summary line
	var totalDuration time.Duration
	for _, r := range results {
		totalDuration += r.ComputeDuration
	}
	avgMs := totalDuration.Milliseconds() / int64(len(results))
	fmt.Fprintf(w, "\nCompleted %d runs in %.1fs (avg: %dms/run)\n",
		len(results), totalDuration.Seconds(), avgMs)
}

// WriteCSV writes results as CSV to the given file path.
func WriteCSV(path string, results []RunResult) error {
	f, err := os.Create(path)
	if err != nil {
		return fmt.Errorf("creating CSV file: %w", err)
	}
	defer f.Close()

	w := csv.NewWriter(f)
	defer w.Flush()

	// Write header
	if err := w.Write(Headers()); err != nil {
		return fmt.Errorf("writing CSV header: %w", err)
	}

	// Write rows
	for i, r := range results {
		if err := w.Write(r.Row(i + 1)); err != nil {
			return fmt.Errorf("writing CSV row %d: %w", i+1, err)
		}
	}

	return nil
}

// SortColumn defines recognized sort column names.
var SortColumn = map[string]bool{
	"distance":    true,
	"distance-3d": true,
	"elev-gain":   true,
	"elev-loss":   true,
	"moving-time": true,
	"avg-speed":   true,
	"max-speed":   true,
	"stops":       true,
	"filtered":    true,
	"time":        true,
}

// SortColumnNames returns all valid sort column names.
func SortColumnNames() string {
	names := make([]string, 0, len(SortColumn))
	for k := range SortColumn {
		names = append(names, k)
	}
	sort.Strings(names)
	return strings.Join(names, ", ")
}

// SortResults sorts results in place by the given column name (ascending).
func SortResults(results []RunResult, column string) {
	sort.SliceStable(results, func(i, j int) bool {
		a, b := results[i], results[j]
		switch column {
		case "distance":
			return a.Distance2D < b.Distance2D
		case "distance-3d":
			return a.Distance3D < b.Distance3D
		case "elev-gain":
			return a.ElevGain < b.ElevGain
		case "elev-loss":
			return a.ElevLoss < b.ElevLoss
		case "moving-time":
			return a.MovingTime < b.MovingTime
		case "avg-speed":
			return a.AvgSpeed < b.AvgSpeed
		case "max-speed":
			return a.MaxSpeed < b.MaxSpeed
		case "stops":
			return a.StopCount < b.StopCount
		case "filtered":
			return a.FilteredPoints < b.FilteredPoints
		case "time":
			return a.ComputeDuration < b.ComputeDuration
		default:
			return false
		}
	})
}
