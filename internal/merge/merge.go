package merge

import (
	"fmt"
	"sort"

	"github.com/jchable/gpx-utility-analyzer/internal/gpx"
)

// Merge combines multiple GPX documents into a single one.
// If sortByTime is true, all points are sorted chronologically.
func Merge(docs []*gpx.GPX, sortByTime bool) (*gpx.GPX, error) {
	if len(docs) == 0 {
		return nil, fmt.Errorf("no GPX documents to merge")
	}

	var allPoints []gpx.TrackPoint
	for _, doc := range docs {
		points, err := doc.AllPoints()
		if err != nil {
			return nil, fmt.Errorf("extracting points: %w", err)
		}
		allPoints = append(allPoints, points...)
	}

	if len(allPoints) == 0 {
		return nil, fmt.Errorf("no trackpoints found across all files")
	}

	if sortByTime {
		sort.Slice(allPoints, func(i, j int) bool {
			return allPoints[i].Time.Before(allPoints[j].Time)
		})
	}

	return gpx.NewGPXFromPoints(allPoints, "merged"), nil
}
