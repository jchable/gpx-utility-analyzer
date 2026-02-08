package stats

import (
	"math"

	"github.com/jchable/gpx-utility-analyzer/cli/internal/gpx"
)

// profilePoint represents a point on the 1D elevation profile.
type profilePoint struct {
	cumDist float64 // cumulative horizontal distance in meters
	ele     float64 // elevation in meters
}

// buildProfile constructs the (cumulative distance, elevation) profile from track points.
func buildProfile(points []gpx.TrackPoint) []profilePoint {
	profile := make([]profilePoint, len(points))
	profile[0] = profilePoint{cumDist: 0, ele: points[0].Ele}

	cumDist := 0.0
	for i := 1; i < len(points); i++ {
		d := Haversine(points[i-1].Lat, points[i-1].Lon, points[i].Lat, points[i].Lon)
		cumDist += d
		profile[i] = profilePoint{cumDist: cumDist, ele: points[i].Ele}
	}
	return profile
}

// perpendicularDistance computes the vertical distance of point p from the line
// segment defined by a and b on the (cumDist, ele) plane.
func perpendicularDistance(p, a, b profilePoint) float64 {
	if a.cumDist == b.cumDist {
		return math.Abs(p.ele - a.ele)
	}
	// Interpolate elevation at p.cumDist along the a→b line
	t := (p.cumDist - a.cumDist) / (b.cumDist - a.cumDist)
	interpolated := a.ele + t*(b.ele-a.ele)
	return math.Abs(p.ele - interpolated)
}

// douglasPeucker applies the Douglas-Peucker simplification to the profile
// and returns the indices of retained points.
func douglasPeucker(profile []profilePoint, epsilon float64) []int {
	if len(profile) < 2 {
		indices := make([]int, len(profile))
		for i := range indices {
			indices[i] = i
		}
		return indices
	}

	// Find the point with the maximum distance from the line first→last
	maxDist := 0.0
	maxIdx := 0
	first := profile[0]
	last := profile[len(profile)-1]

	for i := 1; i < len(profile)-1; i++ {
		d := perpendicularDistance(profile[i], first, last)
		if d > maxDist {
			maxDist = d
			maxIdx = i
		}
	}

	if maxDist > epsilon {
		// Recursively simplify both halves
		left := douglasPeucker(profile[:maxIdx+1], epsilon)
		right := douglasPeucker(profile[maxIdx:], epsilon)

		// Combine, avoiding duplicate at the split point
		result := make([]int, 0, len(left)+len(right)-1)
		result = append(result, left...)
		for _, idx := range right[1:] {
			result = append(result, idx+maxIdx)
		}
		return result
	}

	// All points are within epsilon — keep only endpoints
	return []int{0, len(profile) - 1}
}

// ComputeElevationDP computes elevation gain/loss using Douglas-Peucker simplification
// of the elevation profile. Epsilon is the maximum vertical deviation in meters.
// Max/Min are computed on ALL original points; D+/D- only on the simplified profile.
func ComputeElevationDP(points []gpx.TrackPoint, epsilon float64) ElevationResult {
	if len(points) == 0 {
		return ElevationResult{}
	}

	result := ElevationResult{
		Max: points[0].Ele,
		Min: points[0].Ele,
	}

	// Compute max/min on all original points
	for _, p := range points[1:] {
		if p.Ele > result.Max {
			result.Max = p.Ele
		}
		if p.Ele < result.Min {
			result.Min = p.Ele
		}
	}

	if len(points) < 2 {
		return result
	}

	// Build profile and simplify
	profile := buildProfile(points)
	indices := douglasPeucker(profile, epsilon)

	// Compute D+/D- on simplified profile
	for i := 1; i < len(indices); i++ {
		delta := profile[indices[i]].ele - profile[indices[i-1]].ele
		if delta > 0 {
			result.Gain += delta
		} else {
			result.Loss += -delta
		}
	}

	return result
}
