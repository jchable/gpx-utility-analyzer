package stats

import (
	"math"

	"github.com/jchable/gpx-utility-analyzer/internal/gpx"
)

// ElevationAlgo identifies which elevation algorithm to use.
type ElevationAlgo string

const (
	AlgoThreshold      ElevationAlgo = "threshold"
	AlgoDouglasPeucker ElevationAlgo = "douglas-peucker"
	AlgoSegments       ElevationAlgo = "segments"
)

// ValidAlgo returns true if the given string is a recognized elevation algorithm.
func ValidAlgo(s string) bool {
	switch ElevationAlgo(s) {
	case AlgoThreshold, AlgoDouglasPeucker, AlgoSegments:
		return true
	}
	return false
}

// ElevationConfig holds parameters for elevation algorithms.
type ElevationConfig struct {
	Algo        ElevationAlgo
	Threshold   float64 // used by threshold algo (meters)
	Epsilon     float64 // used by douglas-peucker (meters of vertical deviation)
	MinSegLen   float64 // used by segments algo (meters of horizontal distance)
	MaxSlopeDev float64 // used by segments algo (max RMS residual in meters)
}

// DefaultElevationConfig returns the backward-compatible default.
func DefaultElevationConfig() ElevationConfig {
	return ElevationConfig{
		Algo:        AlgoThreshold,
		Threshold:   2.0,
		Epsilon:     3.0,
		MinSegLen:   200.0,
		MaxSlopeDev: 2.0,
	}
}

// ComputeElevationWithAlgo dispatches to the configured algorithm.
func ComputeElevationWithAlgo(points []gpx.TrackPoint, cfg ElevationConfig) ElevationResult {
	switch cfg.Algo {
	case AlgoDouglasPeucker:
		return ComputeElevationDP(points, cfg.Epsilon)
	case AlgoSegments:
		return ComputeElevationSegments(points, cfg.MinSegLen, cfg.MaxSlopeDev)
	default:
		return ComputeElevation(points, cfg.Threshold)
	}
}

// ElevationResult holds the computed elevation statistics.
type ElevationResult struct {
	Gain float64 // meters, positive
	Loss float64 // meters, stored as positive value
	Max  float64
	Min  float64
}

// ComputeElevation computes elevation gain, loss, max, and min from a slice of TrackPoints.
// The threshold parameter filters GPS noise: only elevation changes >= threshold are counted.
func ComputeElevation(points []gpx.TrackPoint, threshold float64) ElevationResult {
	if len(points) == 0 {
		return ElevationResult{}
	}

	result := ElevationResult{
		Max: points[0].Ele,
		Min: points[0].Ele,
	}

	refEle := points[0].Ele

	for i := 1; i < len(points); i++ {
		ele := points[i].Ele

		if ele > result.Max {
			result.Max = ele
		}
		if ele < result.Min {
			result.Min = ele
		}

		delta := ele - refEle
		if math.Abs(delta) >= threshold {
			if delta > 0 {
				result.Gain += delta
			} else {
				result.Loss += -delta
			}
			refEle = ele
		}
	}

	return result
}
