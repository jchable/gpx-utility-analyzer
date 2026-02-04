package stats

import (
	"math"

	"github.com/jchable/gpx-utility-analyzer/internal/gpx"
)

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
