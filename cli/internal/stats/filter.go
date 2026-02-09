package stats

import (
	"github.com/jchable/gpx-utility-analyzer/cli/internal/gpx"
)

// FilterOutliers removes GPS outlier points whose speed from the last accepted
// point exceeds maxSpeed (m/s). It uses a forward-scan algorithm:
//   - Point 0 is always kept (anchor).
//   - For each subsequent point, compute Haversine(lastAccepted, current) / dt.
//   - If speed > maxSpeed → discard the point; the anchor stays the same.
//   - If speed ≤ maxSpeed → accept, update anchor.
//   - Points with dt ≤ 0 are always kept (can't compute meaningful speed).
//
// Returns the filtered slice and the number of removed points.
// If maxSpeed ≤ 0 or len(points) ≤ 1, returns the original slice unmodified.
func FilterOutliers(points []gpx.TrackPoint, maxSpeed float64) ([]gpx.TrackPoint, int) {
	if maxSpeed <= 0 || len(points) <= 1 {
		return points, 0
	}

	filtered := make([]gpx.TrackPoint, 0, len(points))
	filtered = append(filtered, points[0])
	removed := 0

	for i := 1; i < len(points); i++ {
		anchor := filtered[len(filtered)-1]
		dt := points[i].Time.Sub(anchor.Time).Seconds()

		if dt <= 0 {
			// Can't compute speed — keep the point (simultaneous timestamps).
			filtered = append(filtered, points[i])
			continue
		}

		dist := Haversine(anchor.Lat, anchor.Lon, points[i].Lat, points[i].Lon)
		speed := dist / dt

		if speed > maxSpeed {
			removed++
			continue
		}

		filtered = append(filtered, points[i])
	}

	return filtered, removed
}
