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
