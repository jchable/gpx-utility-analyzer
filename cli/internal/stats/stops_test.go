package stats

import (
	"testing"
	"time"

	"github.com/jchable/gpx-utility-analyzer/cli/internal/gpx"
)

func makeTime(minutesOffset int) time.Time {
	return time.Date(2024, 1, 1, 10, 0, 0, 0, time.UTC).Add(time.Duration(minutesOffset) * time.Minute)
}

func TestDetectStops(t *testing.T) {
	cfg := StopConfig{MaxSpeed: 0.3, MinDuration: 2 * time.Minute}

	t.Run("no stops when always moving", func(t *testing.T) {
		// Points ~200m apart every minute = ~3.3 m/s
		points := []gpx.TrackPoint{
			{Lat: 48.8566, Lon: 2.3522, Time: makeTime(0)},
			{Lat: 48.8580, Lon: 2.3540, Time: makeTime(1)},
			{Lat: 48.8600, Lon: 2.3560, Time: makeTime(2)},
			{Lat: 48.8620, Lon: 2.3580, Time: makeTime(3)},
		}
		EnrichPoints(points)
		stops := DetectStops(points, cfg)
		if len(stops) != 0 {
			t.Errorf("expected 0 stops, got %d", len(stops))
		}
	})

	t.Run("detects a single stop", func(t *testing.T) {
		// Moving, then 3 minutes nearly stationary, then moving
		points := []gpx.TrackPoint{
			{Lat: 48.8566, Lon: 2.3522, Time: makeTime(0)},
			{Lat: 48.8580, Lon: 2.3540, Time: makeTime(1)},  // moving
			{Lat: 48.8580, Lon: 2.3540, Time: makeTime(2)},  // stopped
			{Lat: 48.8580, Lon: 2.3540, Time: makeTime(3)},  // stopped
			{Lat: 48.8580, Lon: 2.3540, Time: makeTime(4)},  // stopped
			{Lat: 48.8600, Lon: 2.3560, Time: makeTime(5)},  // moving again
		}
		EnrichPoints(points)
		stops := DetectStops(points, cfg)
		if len(stops) != 1 {
			t.Fatalf("expected 1 stop, got %d", len(stops))
		}
		if stops[0].Duration < 2*time.Minute {
			t.Errorf("expected stop duration >= 2m, got %v", stops[0].Duration)
		}
	})

	t.Run("ignores short pauses", func(t *testing.T) {
		// Brief 1-minute stop - below MinDuration
		points := []gpx.TrackPoint{
			{Lat: 48.8566, Lon: 2.3522, Time: makeTime(0)},
			{Lat: 48.8580, Lon: 2.3540, Time: makeTime(1)},
			{Lat: 48.8580, Lon: 2.3540, Time: makeTime(2)}, // 1 min stop
			{Lat: 48.8600, Lon: 2.3560, Time: makeTime(3)},
		}
		EnrichPoints(points)
		stops := DetectStops(points, cfg)
		if len(stops) != 0 {
			t.Errorf("expected 0 stops for short pause, got %d", len(stops))
		}
	})
}

func TestDetectStops_MultipleStops(t *testing.T) {
	cfg := StopConfig{MaxSpeed: 0.3, MinDuration: 2 * time.Minute}

	// Moving → stop 1 (3min) → moving → stop 2 (4min) → moving
	points := []gpx.TrackPoint{
		{Lat: 48.8566, Lon: 2.3522, Time: makeTime(0)},  // moving
		{Lat: 48.8580, Lon: 2.3540, Time: makeTime(1)},  // moving
		{Lat: 48.8580, Lon: 2.3540, Time: makeTime(2)},  // stop 1 start
		{Lat: 48.8580, Lon: 2.3540, Time: makeTime(3)},  // stop 1
		{Lat: 48.8580, Lon: 2.3540, Time: makeTime(4)},  // stop 1 end
		{Lat: 48.8600, Lon: 2.3560, Time: makeTime(5)},  // moving
		{Lat: 48.8620, Lon: 2.3580, Time: makeTime(6)},  // moving
		{Lat: 48.8620, Lon: 2.3580, Time: makeTime(7)},  // stop 2 start
		{Lat: 48.8620, Lon: 2.3580, Time: makeTime(8)},  // stop 2
		{Lat: 48.8620, Lon: 2.3580, Time: makeTime(9)},  // stop 2
		{Lat: 48.8620, Lon: 2.3580, Time: makeTime(10)}, // stop 2 end
		{Lat: 48.8640, Lon: 2.3600, Time: makeTime(11)}, // moving
	}
	EnrichPoints(points)
	stops := DetectStops(points, cfg)
	if len(stops) != 2 {
		t.Fatalf("expected 2 stops, got %d", len(stops))
	}
	if stops[0].Duration < 2*time.Minute {
		t.Errorf("stop 1 duration should be >= 2m, got %v", stops[0].Duration)
	}
	if stops[1].Duration < 3*time.Minute {
		t.Errorf("stop 2 duration should be >= 3m, got %v", stops[1].Duration)
	}
}

func TestDetectStops_StopAtEndOfTrack(t *testing.T) {
	cfg := StopConfig{MaxSpeed: 0.3, MinDuration: 2 * time.Minute}

	// Moving, then stops at end of track (no resume)
	points := []gpx.TrackPoint{
		{Lat: 48.8566, Lon: 2.3522, Time: makeTime(0)},
		{Lat: 48.8580, Lon: 2.3540, Time: makeTime(1)},
		{Lat: 48.8580, Lon: 2.3540, Time: makeTime(2)}, // stop
		{Lat: 48.8580, Lon: 2.3540, Time: makeTime(3)}, // stop
		{Lat: 48.8580, Lon: 2.3540, Time: makeTime(4)}, // stop — track ends
	}
	EnrichPoints(points)
	stops := DetectStops(points, cfg)
	if len(stops) != 1 {
		t.Fatalf("expected 1 stop at end, got %d", len(stops))
	}
}

func TestDetectStops_StopAtStart(t *testing.T) {
	cfg := StopConfig{MaxSpeed: 0.3, MinDuration: 2 * time.Minute}

	// Stationary from start, then moving
	points := []gpx.TrackPoint{
		{Lat: 48.8566, Lon: 2.3522, Time: makeTime(0)},
		{Lat: 48.8566, Lon: 2.3522, Time: makeTime(1)}, // stopped
		{Lat: 48.8566, Lon: 2.3522, Time: makeTime(2)}, // stopped
		{Lat: 48.8566, Lon: 2.3522, Time: makeTime(3)}, // stopped
		{Lat: 48.8580, Lon: 2.3540, Time: makeTime(4)}, // start moving
		{Lat: 48.8600, Lon: 2.3560, Time: makeTime(5)},
	}
	EnrichPoints(points)
	stops := DetectStops(points, cfg)
	if len(stops) != 1 {
		t.Fatalf("expected 1 stop at start, got %d", len(stops))
	}
	if stops[0].Duration < 2*time.Minute {
		t.Errorf("stop duration should be >= 2m, got %v", stops[0].Duration)
	}
}

func TestDetectStops_SinglePoint(t *testing.T) {
	cfg := StopConfig{MaxSpeed: 0.3, MinDuration: 2 * time.Minute}
	points := []gpx.TrackPoint{
		{Lat: 48.8566, Lon: 2.3522, Time: makeTime(0)},
	}
	EnrichPoints(points)
	stops := DetectStops(points, cfg)
	if len(stops) != 0 {
		t.Errorf("expected 0 stops for single point, got %d", len(stops))
	}
}

func TestDetectStops_EmptyPoints(t *testing.T) {
	cfg := StopConfig{MaxSpeed: 0.3, MinDuration: 2 * time.Minute}
	stops := DetectStops(nil, cfg)
	if stops != nil {
		t.Errorf("expected nil stops for nil points, got %v", stops)
	}
}

func TestDetectStops_CentroidCalculation(t *testing.T) {
	cfg := StopConfig{MaxSpeed: 0.3, MinDuration: 2 * time.Minute}

	// Stop with slight position drift — centroid should be average
	points := []gpx.TrackPoint{
		{Lat: 48.8566, Lon: 2.3522, Time: makeTime(0)},
		{Lat: 48.8580, Lon: 2.3540, Time: makeTime(1)}, // moving
		{Lat: 48.8580, Lon: 2.3540, Time: makeTime(2)}, // stop
		{Lat: 48.8581, Lon: 2.3541, Time: makeTime(3)}, // stop (slight drift)
		{Lat: 48.8579, Lon: 2.3539, Time: makeTime(4)}, // stop (slight drift)
		{Lat: 48.8600, Lon: 2.3560, Time: makeTime(5)}, // moving
	}
	EnrichPoints(points)
	stops := DetectStops(points, cfg)
	if len(stops) != 1 {
		t.Fatalf("expected 1 stop, got %d", len(stops))
	}
	// Centroid should be near 48.858, 2.354
	if stops[0].Lat < 48.857 || stops[0].Lat > 48.859 {
		t.Errorf("stop centroid lat out of range: %f", stops[0].Lat)
	}
	if stops[0].Lon < 2.353 || stops[0].Lon > 2.355 {
		t.Errorf("stop centroid lon out of range: %f", stops[0].Lon)
	}
}

func TestDetectStops_TrailPreset(t *testing.T) {
	cfg := Presets[PresetTrail] // MaxSpeed=0.3, MinDuration=2min, MaxDistance=50

	// Nearly stationary for 3 minutes — should be a stop
	points := []gpx.TrackPoint{
		{Lat: 48.8566, Lon: 2.3522, Time: makeTime(0)},
		{Lat: 48.8580, Lon: 2.3540, Time: makeTime(1)},   // fast
		{Lat: 48.8580, Lon: 2.35401, Time: makeTime(2)},   // nearly stopped
		{Lat: 48.8580, Lon: 2.35401, Time: makeTime(3)},   // nearly stopped
		{Lat: 48.8580, Lon: 2.35402, Time: makeTime(4)},   // nearly stopped
		{Lat: 48.8600, Lon: 2.3560, Time: makeTime(5)},    // fast
	}
	EnrichPoints(points)
	stops := DetectStops(points, cfg)
	if len(stops) != 1 {
		t.Fatalf("expected 1 stop with trail preset, got %d", len(stops))
	}
}

func TestDetectStops_MaxDistance_RejectsSlowUphill(t *testing.T) {
	// Slow uphill: speed below threshold but covers real distance (200m over 3min)
	cfg := StopConfig{MaxSpeed: 0.5, MinDuration: 2 * time.Minute, MaxDistance: 50}

	points := []gpx.TrackPoint{
		{Lat: 48.8566, Lon: 2.3522, Time: makeTime(0)},
		{Lat: 48.8580, Lon: 2.3540, Time: makeTime(1)},   // fast
		{Lat: 48.8581, Lon: 2.3541, Time: makeTime(2)},   // slow (~11m/60s = 0.18 m/s)
		{Lat: 48.8582, Lon: 2.3542, Time: makeTime(3)},   // slow
		{Lat: 48.8583, Lon: 2.3543, Time: makeTime(4)},   // slow — but 200m from start
		{Lat: 48.8600, Lon: 2.3560, Time: makeTime(5)},   // fast
	}
	EnrichPoints(points)
	stops := DetectStops(points, cfg)

	// Should NOT be a stop: displacement from first to last slow point exceeds MaxDistance
	for i, stop := range stops {
		dist := Haversine(points[2].Lat, points[2].Lon, points[4].Lat, points[4].Lon)
		if dist > cfg.MaxDistance {
			t.Errorf("stop %d (duration=%v) with displacement %.0fm should have been rejected (MaxDistance=%.0f)", i, stop.Duration, dist, cfg.MaxDistance)
		}
	}
}

func TestDetectStops_MaxDistance_AcceptsRealStop(t *testing.T) {
	// Real stop: GPS drift but person stays in same spot
	cfg := StopConfig{MaxSpeed: 0.5, MinDuration: 2 * time.Minute, MaxDistance: 50}

	points := []gpx.TrackPoint{
		{Lat: 48.8566, Lon: 2.3522, Time: makeTime(0)},
		{Lat: 48.8580, Lon: 2.3540, Time: makeTime(1)},    // fast
		{Lat: 48.8580, Lon: 2.3540, Time: makeTime(2)},    // stopped
		{Lat: 48.85801, Lon: 2.35401, Time: makeTime(3)},   // stopped (tiny drift)
		{Lat: 48.8580, Lon: 2.3540, Time: makeTime(4)},    // stopped
		{Lat: 48.8600, Lon: 2.3560, Time: makeTime(5)},    // fast
	}
	EnrichPoints(points)
	stops := DetectStops(points, cfg)
	if len(stops) != 1 {
		t.Fatalf("expected 1 stop (real stop with GPS drift), got %d", len(stops))
	}
}

func TestTotalStopTime(t *testing.T) {
	stops := []Stop{
		{Duration: 5 * time.Minute},
		{Duration: 10 * time.Minute},
		{Duration: 3 * time.Minute},
	}
	total := TotalStopTime(stops)
	if total != 18*time.Minute {
		t.Errorf("expected 18m, got %v", total)
	}
}

func TestTotalStopTime_Empty(t *testing.T) {
	total := TotalStopTime(nil)
	if total != 0 {
		t.Errorf("expected 0, got %v", total)
	}
}

func TestLongestStop(t *testing.T) {
	stops := []Stop{
		{Duration: 5 * time.Minute},
		{Duration: 15 * time.Minute},
		{Duration: 3 * time.Minute},
	}
	longest := LongestStop(stops)
	if longest == nil {
		t.Fatal("expected non-nil longest stop")
	}
	if longest.Duration != 15*time.Minute {
		t.Errorf("expected 15m, got %v", longest.Duration)
	}
}

func TestLongestStop_Empty(t *testing.T) {
	longest := LongestStop(nil)
	if longest != nil {
		t.Error("expected nil for empty stops")
	}
}

func TestAvgStopDuration(t *testing.T) {
	stops := []Stop{
		{Duration: 6 * time.Minute},
		{Duration: 12 * time.Minute},
	}
	avg := AvgStopDuration(stops)
	if avg != 9*time.Minute {
		t.Errorf("expected 9m, got %v", avg)
	}
}

func TestAvgStopDuration_Empty(t *testing.T) {
	avg := AvgStopDuration(nil)
	if avg != 0 {
		t.Errorf("expected 0, got %v", avg)
	}
}

func TestPresets(t *testing.T) {
	for _, name := range []string{PresetHiking, PresetTrail, PresetCycling} {
		preset, ok := Presets[name]
		if !ok {
			t.Errorf("preset %q not found", name)
			continue
		}
		if preset.MaxSpeed <= 0 {
			t.Errorf("preset %q MaxSpeed should be > 0", name)
		}
		if preset.MinDuration <= 0 {
			t.Errorf("preset %q MinDuration should be > 0", name)
		}
	}
}

func TestPresets_HikingMostSensitive(t *testing.T) {
	hiking := Presets[PresetHiking]
	trail := Presets[PresetTrail]
	cycling := Presets[PresetCycling]

	// Hiking should have the lowest stop speed threshold
	if hiking.MaxSpeed >= trail.MaxSpeed {
		t.Error("hiking MaxSpeed should be < trail MaxSpeed")
	}
	if trail.MaxSpeed >= cycling.MaxSpeed {
		t.Error("trail MaxSpeed should be < cycling MaxSpeed")
	}

	// Hiking should have the longest MinDuration
	if hiking.MinDuration <= trail.MinDuration {
		t.Error("hiking MinDuration should be > trail MinDuration")
	}
	if trail.MinDuration <= cycling.MinDuration {
		t.Error("trail MinDuration should be > cycling MinDuration")
	}
}
