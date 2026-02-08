package dem

import "github.com/jchable/gpx-utility-analyzer/cli/internal/gpx"

// CorrectElevations replaces TrackPoint.Ele with DEM elevation where available.
// Points where the tile is missing or void keep their original GPS elevation.
func CorrectElevations(points []gpx.TrackPoint, src *Source) {
	for i := range points {
		if ele, ok := src.Lookup(points[i].Lat, points[i].Lon); ok {
			points[i].Ele = ele
		}
	}
}
