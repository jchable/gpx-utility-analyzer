package stats

import (
	"math"

	"github.com/jchable/gpx-utility-analyzer/cli/internal/gpx"
)

// segment represents a portion of the elevation profile with a linear fit.
type segment struct {
	startIdx int
	endIdx   int
	startEle float64 // fitted elevation at segment start
	endEle   float64 // fitted elevation at segment end
}

// linearFit computes slope and intercept for the profile slice using least squares.
// x = cumDist, y = ele. Returns (slope, intercept).
func linearFit(profile []profilePoint) (float64, float64) {
	n := float64(len(profile))
	if n < 2 {
		return 0, profile[0].ele
	}

	var sumX, sumY, sumXY, sumX2 float64
	for _, p := range profile {
		sumX += p.cumDist
		sumY += p.ele
		sumXY += p.cumDist * p.ele
		sumX2 += p.cumDist * p.cumDist
	}

	denom := n*sumX2 - sumX*sumX
	if math.Abs(denom) < 1e-12 {
		return 0, sumY / n
	}

	slope := (n*sumXY - sumX*sumY) / denom
	intercept := (sumY - slope*sumX) / n
	return slope, intercept
}

// rmsResidual computes the RMS of vertical residuals from the linear fit.
func rmsResidual(profile []profilePoint, slope, intercept float64) float64 {
	if len(profile) == 0 {
		return 0
	}
	var sumSq float64
	for _, p := range profile {
		predicted := slope*p.cumDist + intercept
		residual := p.ele - predicted
		sumSq += residual * residual
	}
	return math.Sqrt(sumSq / float64(len(profile)))
}

// findSegments partitions the profile into segments of approximately constant slope.
// minSegLen is the minimum horizontal distance of a segment in meters.
// maxSlopeDev is the maximum RMS residual in meters before cutting.
func findSegments(profile []profilePoint, minSegLen, maxSlopeDev float64) []segment {
	if len(profile) < 2 {
		return nil
	}

	var segments []segment
	segStart := 0

	for segStart < len(profile)-1 {
		segEnd := segStart + 1

		// Extend the segment as far as possible
		for segEnd < len(profile) {
			segLen := profile[segEnd].cumDist - profile[segStart].cumDist
			sub := profile[segStart : segEnd+1]
			slope, intercept := linearFit(sub)
			rms := rmsResidual(sub, slope, intercept)

			if segLen >= minSegLen && rms > maxSlopeDev {
				// Cut: the segment up to segEnd-1 is the valid segment
				segEnd--
				break
			}
			segEnd++
		}

		// Clamp segEnd
		if segEnd >= len(profile) {
			segEnd = len(profile) - 1
		}
		if segEnd <= segStart {
			segEnd = segStart + 1
			if segEnd >= len(profile) {
				break
			}
		}

		// Fit the final segment
		sub := profile[segStart : segEnd+1]
		slope, intercept := linearFit(sub)
		startEle := slope*profile[segStart].cumDist + intercept
		endEle := slope*profile[segEnd].cumDist + intercept

		segments = append(segments, segment{
			startIdx: segStart,
			endIdx:   segEnd,
			startEle: startEle,
			endEle:   endEle,
		})

		segStart = segEnd
	}

	return segments
}

// ComputeElevationSegments computes elevation gain/loss by partitioning the profile
// into segments of approximately constant slope and computing D+/D- on the fitted endpoints.
// Max/Min are computed on all original points.
func ComputeElevationSegments(points []gpx.TrackPoint, minSegLen, maxSlopeDev float64) ElevationResult {
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

	profile := buildProfile(points)
	segs := findSegments(profile, minSegLen, maxSlopeDev)

	// D+/D- from fitted segment endpoints
	for _, seg := range segs {
		delta := seg.endEle - seg.startEle
		if delta > 0 {
			result.Gain += delta
		} else {
			result.Loss += -delta
		}
	}

	return result
}
