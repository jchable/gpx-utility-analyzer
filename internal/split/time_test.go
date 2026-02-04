package split

import (
	"testing"
	"time"

	"github.com/jchable/gpx-utility-analyzer/internal/gpx"
)

func makePoint(hoursOffset int) gpx.TrackPoint {
	return gpx.TrackPoint{
		Lat:  48.8566,
		Lon:  2.3522,
		Time: time.Date(2024, 1, 1, 0, 0, 0, 0, time.UTC).Add(time.Duration(hoursOffset) * time.Hour),
	}
}

func TestByTime(t *testing.T) {
	t.Run("splits into two 24h segments", func(t *testing.T) {
		points := []gpx.TrackPoint{
			makePoint(0),  // day 1
			makePoint(6),  // day 1
			makePoint(12), // day 1
			makePoint(25), // day 2
			makePoint(30), // day 2
		}

		segments, err := ByTime(points, 24*time.Hour)
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		if len(segments) != 2 {
			t.Fatalf("expected 2 segments, got %d", len(segments))
		}
		if len(segments[0].Points) != 3 {
			t.Errorf("segment 0: expected 3 points, got %d", len(segments[0].Points))
		}
		// Second segment has boundary point duplicated + 2 own points
		if len(segments[1].Points) < 2 {
			t.Errorf("segment 1: expected at least 2 points, got %d", len(segments[1].Points))
		}
	})

	t.Run("single segment when all within interval", func(t *testing.T) {
		points := []gpx.TrackPoint{
			makePoint(0),
			makePoint(6),
			makePoint(12),
		}
		segments, err := ByTime(points, 24*time.Hour)
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		if len(segments) != 1 {
			t.Fatalf("expected 1 segment, got %d", len(segments))
		}
		if len(segments[0].Points) != 3 {
			t.Errorf("expected 3 points, got %d", len(segments[0].Points))
		}
	})

	t.Run("error on empty points", func(t *testing.T) {
		_, err := ByTime(nil, 24*time.Hour)
		if err == nil {
			t.Error("expected error for empty points")
		}
	})

	t.Run("error on zero interval", func(t *testing.T) {
		points := []gpx.TrackPoint{makePoint(0)}
		_, err := ByTime(points, 0)
		if err == nil {
			t.Error("expected error for zero interval")
		}
	})
}
