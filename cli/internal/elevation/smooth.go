package elevation

import (
	"sort"

	"github.com/jchable/gpx-utility-analyzer/internal/gpx"
)

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
func SmoothElevations(points []gpx.TrackPoint, level SmoothingLevel) {
	params, ok := Presets[level]
	if !ok || level == SmoothNone {
		return
	}
	elevations := extractElevations(points)
	elevations = medianFilter(elevations, params.MedianWindow)
	elevations = movingAverage(elevations, params.AverageWindow)
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
