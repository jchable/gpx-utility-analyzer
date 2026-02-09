package elevation

import (
	"github.com/jchable/gpx-utility-analyzer/cli/internal/gpx"
)

// TrackSmoothingLevel represents a named track smoothing preset for lat/lon.
type TrackSmoothingLevel string

const (
	TrackSmoothNone   TrackSmoothingLevel = "none"
	TrackSmoothLight  TrackSmoothingLevel = "light"
	TrackSmoothMedium TrackSmoothingLevel = "medium"
	TrackSmoothHeavy  TrackSmoothingLevel = "heavy"
)

// trackSmoothingWindows maps levels to moving average window sizes for lat/lon.
var trackSmoothingWindows = map[TrackSmoothingLevel]int{
	TrackSmoothNone:   0,
	TrackSmoothLight:  3,
	TrackSmoothMedium: 5,
	TrackSmoothHeavy:  9,
}

// ValidTrackSmoothingLevel returns true if the given string is a valid track smoothing level.
func ValidTrackSmoothingLevel(s string) bool {
	_, ok := trackSmoothingWindows[TrackSmoothingLevel(s)]
	return ok
}

// SmoothTrack applies a moving average to the Lat and Lon fields of the given
// points, reducing horizontal GPS noise. This should be applied BEFORE DEM
// correction so that DEM lookups use smoothed coordinates.
// Smoothing is applied independently within time-continuous segments to avoid
// bleed across large time gaps (overnight camps, filtered outlier gaps).
// With TrackSmoothNone, it is a no-op.
func SmoothTrack(points []gpx.TrackPoint, level TrackSmoothingLevel) {
	window, ok := trackSmoothingWindows[level]
	if !ok || window <= 1 {
		return
	}

	times := extractTimes(points)
	breaks := gapIndices(times, GapThreshold)

	lats := make([]float64, len(points))
	lons := make([]float64, len(points))
	for i, p := range points {
		lats[i] = p.Lat
		lons[i] = p.Lon
	}

	lats = movingAverageSegmented(lats, window, breaks)
	lons = movingAverageSegmented(lons, window, breaks)

	for i := range points {
		points[i].Lat = lats[i]
		points[i].Lon = lons[i]
	}
}
