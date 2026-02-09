package stats

import (
	"time"

	"github.com/jchable/gpx-utility-analyzer/cli/internal/gpx"
)

// SpeedResult holds computed speed and pace statistics.
type SpeedResult struct {
	AvgSpeed       float64       // m/s over total time
	AvgMovingSpeed float64       // m/s over moving time
	MaxSpeed       float64       // m/s
	AvgPace        time.Duration // per km, over total time
	AvgMovingPace  time.Duration // per km, over moving time
}

// ComputeSpeed computes speed and pace statistics.
// totalDist is in meters, totalTime and movingTime are durations.
func ComputeSpeed(totalDist float64, totalTime, movingTime time.Duration) SpeedResult {
	var result SpeedResult

	totalSec := totalTime.Seconds()
	movingSec := movingTime.Seconds()

	if totalSec > 0 {
		result.AvgSpeed = totalDist / totalSec
		distKm := totalDist / 1000
		if distKm > 0 {
			result.AvgPace = time.Duration(totalSec/distKm) * time.Second
		}
	}

	if movingSec > 0 {
		result.AvgMovingSpeed = totalDist / movingSec
		distKm := totalDist / 1000
		if distKm > 0 {
			result.AvgMovingPace = time.Duration(movingSec/distKm) * time.Second
		}
	}

	return result
}

// EnrichPoints computes distance from previous point and calculated speed
// for each point in the slice. It modifies points in place.
func EnrichPoints(points []gpx.TrackPoint) {
	for i := 1; i < len(points); i++ {
		dist := Haversine(
			points[i-1].Lat, points[i-1].Lon,
			points[i].Lat, points[i].Lon,
		)
		points[i].DistFromPrev = dist

		dt := points[i].Time.Sub(points[i-1].Time).Seconds()
		if dt > 0 {
			points[i].CalcSpeed = dist / dt
		}
	}
}

// DefaultMaxReasonableSpeed is the fallback (25 m/s ≈ 90 km/h) when no
// preset-specific limit is configured. Points exceeding this speed are
// physically removed from the trace by FilterOutliers before any computation.
const DefaultMaxReasonableSpeed = 25.0 // m/s

// PresetMaxSpeed defines per-preset GPS outlier removal thresholds (m/s).
// Points exceeding these speeds are physically removed from the trace.
var PresetMaxSpeed = map[string]float64{
	PresetHiking:  4.0,  // ~14.4 km/h — brisk walk / scramble
	PresetTrail:   7.0,  // ~25.2 km/h — fast downhill trail running
	PresetCycling: 25.0, // ~90 km/h — fast descents on road
}

// MaxSpeedFromPoints returns the maximum calculated speed from enriched points.
// Outlier points must already be removed by FilterOutliers before calling this.
func MaxSpeedFromPoints(points []gpx.TrackPoint) float64 {
	var max float64
	for _, p := range points {
		if p.CalcSpeed > max {
			max = p.CalcSpeed
		}
	}
	return max
}
