package stats

import (
	"testing"
	"time"

	"github.com/jchable/gpx-utility-analyzer/cli/internal/gpx"
)

func makePoint(lat, lon float64, t time.Time) gpx.TrackPoint {
	return gpx.TrackPoint{Lat: lat, Lon: lon, Ele: 100, Time: t}
}

func TestFilterOutliers_TeleportSpike(t *testing.T) {
	t0 := time.Date(2023, 8, 1, 10, 0, 0, 0, time.UTC)
	points := []gpx.TrackPoint{
		makePoint(48.0, 2.0, t0),
		makePoint(48.0001, 2.0001, t0.Add(10*time.Second)), // ~14 m in 10s = 1.4 m/s — OK
		makePoint(49.0, 3.0, t0.Add(20*time.Second)),       // ~130 km in 10s — teleport!
		makePoint(48.0002, 2.0002, t0.Add(30*time.Second)), // ~14 m from anchor[1] in 20s = 0.7 m/s — OK
		makePoint(48.0001, 2.0001, t0.Add(40*time.Second)), // ~14 m from anchor[3] in 10s — OK
	}

	filtered, removed := FilterOutliers(points, 7.0) // trail preset

	if removed != 1 {
		t.Errorf("expected 1 removed (the teleport), got %d", removed)
	}
	if len(filtered) != 4 {
		t.Errorf("expected 4 remaining points, got %d", len(filtered))
	}
}

func TestFilterOutliers_NormalTrack(t *testing.T) {
	t0 := time.Date(2023, 8, 1, 10, 0, 0, 0, time.UTC)
	// Points moving at ~1.5 m/s (normal walking)
	points := []gpx.TrackPoint{
		makePoint(48.0, 2.0, t0),
		makePoint(48.00001, 2.00001, t0.Add(1*time.Second)),
		makePoint(48.00002, 2.00002, t0.Add(2*time.Second)),
		makePoint(48.00003, 2.00003, t0.Add(3*time.Second)),
	}

	filtered, removed := FilterOutliers(points, 4.0) // hiking preset

	if removed != 0 {
		t.Errorf("expected 0 removed for normal track, got %d", removed)
	}
	if len(filtered) != len(points) {
		t.Errorf("expected all %d points preserved, got %d", len(points), len(filtered))
	}
}

func TestFilterOutliers_ConsecutiveOutliers(t *testing.T) {
	t0 := time.Date(2023, 8, 1, 10, 0, 0, 0, time.UTC)
	points := []gpx.TrackPoint{
		makePoint(48.0, 2.0, t0),                             // anchor
		makePoint(50.0, 5.0, t0.Add(5*time.Second)),          // outlier 1
		makePoint(51.0, 6.0, t0.Add(10*time.Second)),         // outlier 2
		makePoint(52.0, 7.0, t0.Add(15*time.Second)),         // outlier 3
		makePoint(48.00001, 2.00001, t0.Add(20*time.Second)), // back near anchor — kept
	}

	filtered, removed := FilterOutliers(points, 7.0)

	if removed != 3 {
		t.Errorf("expected 3 consecutive outliers removed, got %d", removed)
	}
	if len(filtered) != 2 {
		t.Errorf("expected 2 remaining points, got %d", len(filtered))
	}
}

func TestFilterOutliers_ZeroDeltaTime(t *testing.T) {
	t0 := time.Date(2023, 8, 1, 10, 0, 0, 0, time.UTC)
	// Two points at the same timestamp — should be kept (dt=0, can't compute speed)
	points := []gpx.TrackPoint{
		makePoint(48.0, 2.0, t0),
		makePoint(49.0, 3.0, t0), // same time, far away — but dt=0 → kept
	}

	filtered, removed := FilterOutliers(points, 4.0)

	if removed != 0 {
		t.Errorf("expected 0 removed for zero dt, got %d", removed)
	}
	if len(filtered) != 2 {
		t.Errorf("expected both points kept, got %d", len(filtered))
	}
}

func TestFilterOutliers_Disabled(t *testing.T) {
	t0 := time.Date(2023, 8, 1, 10, 0, 0, 0, time.UTC)
	points := []gpx.TrackPoint{
		makePoint(48.0, 2.0, t0),
		makePoint(50.0, 5.0, t0.Add(1*time.Second)), // insane speed
	}

	// maxSpeed = 0 → disabled
	filtered, removed := FilterOutliers(points, 0)

	if removed != 0 {
		t.Errorf("expected 0 removed when disabled, got %d", removed)
	}
	if len(filtered) != 2 {
		t.Errorf("expected all points when disabled, got %d", len(filtered))
	}
}

func TestFilterOutliers_EmptyAndNil(t *testing.T) {
	filtered, removed := FilterOutliers(nil, 4.0)
	if removed != 0 || filtered != nil {
		t.Errorf("expected nil/0 for nil input, got %v/%d", filtered, removed)
	}

	filtered, removed = FilterOutliers([]gpx.TrackPoint{}, 4.0)
	if removed != 0 || len(filtered) != 0 {
		t.Errorf("expected empty/0 for empty input, got %d/%d", len(filtered), removed)
	}
}

func TestFilterOutliers_LargeTimeGapLowSpeed(t *testing.T) {
	t0 := time.Date(2023, 8, 1, 10, 0, 0, 0, time.UTC)
	// 10 km in 2 hours = 1.39 m/s — perfectly reasonable, must be kept
	points := []gpx.TrackPoint{
		makePoint(48.0, 2.0, t0),
		makePoint(48.09, 2.0, t0.Add(2*time.Hour)), // ~10 km north
	}

	filtered, removed := FilterOutliers(points, 4.0)

	if removed != 0 {
		t.Errorf("expected 0 removed for large time gap with low speed, got %d", removed)
	}
	if len(filtered) != 2 {
		t.Errorf("expected both points kept, got %d", len(filtered))
	}
}
