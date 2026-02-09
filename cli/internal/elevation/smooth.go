package elevation

import (
	"sort"
	"time"

	"github.com/jchable/gpx-utility-analyzer/cli/internal/gpx"
)

// GapThreshold is the minimum time gap between consecutive points that
// indicates a discontinuity (overnight camp, segment boundary, etc.).
// Smoothing and enrichment treat points across such gaps as separate segments.
// Set to 10 minutes to support low-frequency GPS recording (up to 5-min intervals)
// while still catching overnight camps and multi-day gaps.
const GapThreshold = 10 * time.Minute

// SmoothingLevel represents a named smoothing preset.
type SmoothingLevel string

const (
	SmoothNone   SmoothingLevel = "none"
	SmoothLight  SmoothingLevel = "light"
	SmoothMedium SmoothingLevel = "medium"
	SmoothHeavy  SmoothingLevel = "heavy"
)

// SmoothingParams holds window sizes for the two-pass filter.
type SmoothingParams struct {
	MedianWindow  int
	AverageWindow int
}

// Presets maps level names to their parameters.
var Presets = map[SmoothingLevel]SmoothingParams{
	SmoothNone:   {0, 0},
	SmoothLight:  {3, 3},
	SmoothMedium: {5, 5},
	SmoothHeavy:  {7, 11},
}

// ValidLevel returns true if the given string is a valid smoothing level.
func ValidLevel(s string) bool {
	_, ok := Presets[SmoothingLevel(s)]
	return ok
}

// SmoothElevations applies median then moving average filters to the Ele field
// of the given points, modifying them in place. With SmoothNone, it is a no-op.
// Smoothing is applied independently within time-continuous segments to avoid
// bleed across large time gaps (overnight camps, filtered outlier gaps).
func SmoothElevations(points []gpx.TrackPoint, level SmoothingLevel) {
	params, ok := Presets[level]
	if !ok || level == SmoothNone {
		return
	}
	times := extractTimes(points)
	breaks := gapIndices(times, GapThreshold)

	elevations := extractElevations(points)
	elevations = medianFilterSegmented(elevations, params.MedianWindow, breaks)
	elevations = movingAverageSegmented(elevations, params.AverageWindow, breaks)
	applyElevations(points, elevations)
}

// medianFilter replaces each value with the median of a centered window.
// Removes outlier spikes while preserving legitimate elevation changes.
func medianFilter(data []float64, windowSize int) []float64 {
	if windowSize <= 1 || len(data) == 0 {
		return data
	}
	result := make([]float64, len(data))
	half := windowSize / 2
	for i := range data {
		start := i - half
		if start < 0 {
			start = 0
		}
		end := i + half
		if end >= len(data) {
			end = len(data) - 1
		}
		window := make([]float64, end-start+1)
		copy(window, data[start:end+1])
		sort.Float64s(window)
		result[i] = window[len(window)/2]
	}
	return result
}

// movingAverage replaces each value with the mean of a centered window.
// Smooths high-frequency noise after spikes have been removed by median filter.
func movingAverage(data []float64, windowSize int) []float64 {
	if windowSize <= 1 || len(data) == 0 {
		return data
	}
	result := make([]float64, len(data))
	half := windowSize / 2
	for i := range data {
		start := i - half
		if start < 0 {
			start = 0
		}
		end := i + half
		if end >= len(data) {
			end = len(data) - 1
		}
		sum := 0.0
		for j := start; j <= end; j++ {
			sum += data[j]
		}
		result[i] = sum / float64(end-start+1)
	}
	return result
}

func extractElevations(points []gpx.TrackPoint) []float64 {
	elev := make([]float64, len(points))
	for i, p := range points {
		elev[i] = p.Ele
	}
	return elev
}

func applyElevations(points []gpx.TrackPoint, elev []float64) {
	for i := range points {
		points[i].Ele = elev[i]
	}
}

// extractTimes returns the Time field of each point.
func extractTimes(points []gpx.TrackPoint) []time.Time {
	times := make([]time.Time, len(points))
	for i, p := range points {
		times[i] = p.Time
	}
	return times
}

// gapIndices returns the indices where a time gap > threshold occurs.
// Each returned index marks the START of a new segment (the point after the gap).
func gapIndices(times []time.Time, threshold time.Duration) []int {
	var breaks []int
	for i := 1; i < len(times); i++ {
		if times[i].Sub(times[i-1]) > threshold {
			breaks = append(breaks, i)
		}
	}
	return breaks
}

// movingAverageSegmented applies movingAverage independently within each
// time-continuous segment defined by breaks. breaks contains the start indices
// of new segments (as returned by gapIndices).
func movingAverageSegmented(data []float64, windowSize int, breaks []int) []float64 {
	if windowSize <= 1 || len(data) == 0 {
		return data
	}
	if len(breaks) == 0 {
		return movingAverage(data, windowSize)
	}
	result := make([]float64, len(data))
	start := 0
	for _, brk := range breaks {
		smoothed := movingAverage(data[start:brk], windowSize)
		copy(result[start:brk], smoothed)
		start = brk
	}
	smoothed := movingAverage(data[start:], windowSize)
	copy(result[start:], smoothed)
	return result
}

// medianFilterSegmented applies medianFilter independently within each
// time-continuous segment defined by breaks.
func medianFilterSegmented(data []float64, windowSize int, breaks []int) []float64 {
	if windowSize <= 1 || len(data) == 0 {
		return data
	}
	if len(breaks) == 0 {
		return medianFilter(data, windowSize)
	}
	result := make([]float64, len(data))
	start := 0
	for _, brk := range breaks {
		filtered := medianFilter(data[start:brk], windowSize)
		copy(result[start:brk], filtered)
		start = brk
	}
	filtered := medianFilter(data[start:], windowSize)
	copy(result[start:], filtered)
	return result
}
