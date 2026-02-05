package split

import (
	"fmt"
	"time"

	"github.com/jchable/gpx-utility-analyzer/internal/gpx"
)

// TimeSegment represents a slice of points within a time interval.
type TimeSegment struct {
	Index     int              // 0-based segment index
	StartTime time.Time        // start of the time interval
	EndTime   time.Time        // end of the time interval
	Points    []gpx.TrackPoint // points in this segment
}

// ByTime splits trackpoints into segments based on a time interval.
// Points are assigned to buckets [start, start+interval), [start+interval, start+2*interval), etc.
// Boundary points are duplicated into both adjacent segments for continuity.
func ByTime(points []gpx.TrackPoint, interval time.Duration) ([]TimeSegment, error) {
	if len(points) == 0 {
		return nil, fmt.Errorf("no points to split")
	}
	if interval <= 0 {
		return nil, fmt.Errorf("interval must be positive, got %v", interval)
	}

	start := points[0].Time
	var segments []TimeSegment

	segIdx := 0
	segStart := start
	segEnd := segStart.Add(interval)

	var current []gpx.TrackPoint

	for i, p := range points {
		// Move to the correct bucket
		for p.Time.After(segEnd) || p.Time.Equal(segEnd) {
			if len(current) > 0 {
				segments = append(segments, TimeSegment{
					Index:     segIdx,
					StartTime: segStart,
					EndTime:   segEnd,
					Points:    current,
				})
			}
			segIdx++
			segStart = segEnd
			segEnd = segStart.Add(interval)
			current = nil

			// Duplicate previous point as first point of new segment for continuity
			if i > 0 && len(segments) > 0 {
				prev := points[i-1]
				current = append(current, prev)
			}
		}

		current = append(current, p)
	}

	// Flush last segment
	if len(current) > 0 {
		segments = append(segments, TimeSegment{
			Index:     segIdx,
			StartTime: segStart,
			EndTime:   segEnd,
			Points:    current,
		})
	}

	return segments, nil
}
