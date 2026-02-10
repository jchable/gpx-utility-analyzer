package benchmark

import (
	"fmt"
	"time"
)

// RunResult holds metrics from a single benchmark run.
type RunResult struct {
	Combination     Combination
	Distance2D      float64       // km
	Distance3D      float64       // km
	ElevGain        float64       // m
	ElevLoss        float64       // m
	ElevMax         float64       // m
	ElevMin         float64       // m
	MovingTime      time.Duration
	StoppedTime     time.Duration
	StopCount       int
	AvgSpeed        float64       // km/h
	MaxSpeed        float64       // km/h
	FilteredPoints  int
	ComputeDuration time.Duration
}

// Headers returns column headers for table/CSV output.
func Headers() []string {
	return []string{
		"#",
		"Preset",
		"Algo",
		"E.Smooth",
		"T.Smooth",
		"DEM",
		"Params",
		"Dist 2D",
		"Dist 3D",
		"D+",
		"D-",
		"Max Ele",
		"Min Ele",
		"Moving",
		"Stopped",
		"Stops",
		"Avg Spd",
		"Max Spd",
		"Filtered",
		"Time",
	}
}

// Row returns the result values as strings for table/CSV output.
func (r RunResult) Row(index int) []string {
	return []string{
		fmt.Sprintf("%d", index),
		r.Combination.Preset,
		string(r.Combination.ElevAlgo),
		string(r.Combination.ElevSmoothing),
		string(r.Combination.TrackSmoothing),
		boolYesNo(r.Combination.UseDEM),
		r.Combination.ParamsLabel(),
		fmt.Sprintf("%.2f km", r.Distance2D),
		fmt.Sprintf("%.2f km", r.Distance3D),
		fmt.Sprintf("+%.0f m", r.ElevGain),
		fmt.Sprintf("-%.0f m", r.ElevLoss),
		fmt.Sprintf("%.0f m", r.ElevMax),
		fmt.Sprintf("%.0f m", r.ElevMin),
		formatDuration(r.MovingTime),
		formatDuration(r.StoppedTime),
		fmt.Sprintf("%d", r.StopCount),
		fmt.Sprintf("%.1f km/h", r.AvgSpeed),
		fmt.Sprintf("%.1f km/h", r.MaxSpeed),
		fmt.Sprintf("%d", r.FilteredPoints),
		fmt.Sprintf("%dms", r.ComputeDuration.Milliseconds()),
	}
}

func boolYesNo(b bool) string {
	if b {
		return "yes"
	}
	return "no"
}

func formatDuration(d time.Duration) string {
	h := int(d.Hours())
	m := int(d.Minutes()) % 60
	s := int(d.Seconds()) % 60
	if h > 0 {
		return fmt.Sprintf("%dh%02dm", h, m)
	}
	return fmt.Sprintf("%dm%02ds", m, s)
}
