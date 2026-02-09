package stats

import (
	"testing"
	"time"

	"github.com/jchable/gpx-utility-analyzer/cli/internal/elevation"
	"github.com/jchable/gpx-utility-analyzer/cli/internal/gpx"
)

func TestCompute_SmallGPX(t *testing.T) {
	g, err := gpx.ParseFile("../../testdata/small.gpx")
	if err != nil {
		t.Fatalf("ParseFile: %v", err)
	}
	points, err := g.AllPoints()
	if err != nil {
		t.Fatalf("AllPoints: %v", err)
	}

	cfg := DefaultConfig()
	summary, _, err := Compute(points, g.SegmentCount(), cfg)
	if err != nil {
		t.Fatalf("Compute: %v", err)
	}

	// 5 points
	if summary.PointCount != 5 {
		t.Errorf("expected 5 points, got %d", summary.PointCount)
	}
	if summary.SegmentCount != 1 {
		t.Errorf("expected 1 segment, got %d", summary.SegmentCount)
	}

	// Distance should be > 0 (points are ~200m apart × 4 intervals ≈ 800m)
	if summary.TotalDistance < 500 || summary.TotalDistance > 2000 {
		t.Errorf("TotalDistance out of range: %f m", summary.TotalDistance)
	}
	if summary.TotalDistance3D <= 0 {
		t.Errorf("TotalDistance3D should be > 0, got %f", summary.TotalDistance3D)
	}
	// 3D distance should be >= 2D distance
	if summary.TotalDistance3D < summary.TotalDistance {
		t.Error("TotalDistance3D should be >= TotalDistance")
	}

	// Elevation: points go from 35 to 50 with a dip at 38
	if summary.Elevation.Max < 42 {
		t.Errorf("MaxElevation should be >= 42, got %f", summary.Elevation.Max)
	}
	if summary.Elevation.Min > 40 {
		t.Errorf("MinElevation should be <= 40, got %f", summary.Elevation.Min)
	}

	// Time: 20 minutes total
	if summary.TotalTime != 20*time.Minute {
		t.Errorf("TotalTime expected 20m, got %v", summary.TotalTime)
	}

	// Speed should be > 0
	if summary.Speed.AvgSpeed <= 0 {
		t.Error("AvgSpeed should be > 0")
	}
	if summary.Speed.MaxSpeed <= 0 {
		t.Error("MaxSpeed should be > 0")
	}

	// PointsPerKm should be reasonable
	if summary.PointsPerKm <= 0 {
		t.Error("PointsPerKm should be > 0")
	}
}

func TestCompute_RealisticGPX(t *testing.T) {
	g, err := gpx.ParseFile("../../testdata/trail-realistic.gpx")
	if err != nil {
		t.Fatalf("ParseFile: %v", err)
	}
	points, err := g.AllPoints()
	if err != nil {
		t.Fatalf("AllPoints: %v", err)
	}

	cfg := ComputeConfig{
		ElevationThreshold: 2.0,
		StopConfig:         Presets[PresetTrail],
		SmoothingLevel:     elevation.SmoothMedium,
		MaxReasonableSpeed: PresetMaxSpeed[PresetTrail],
	}
	summary, _, err := Compute(points, g.SegmentCount(), cfg)
	if err != nil {
		t.Fatalf("Compute: %v", err)
	}

	// 110 points
	if summary.PointCount != 110 {
		t.Errorf("expected 110 points, got %d", summary.PointCount)
	}

	// Distance should be in reasonable range for 110 points ~45m apart
	// ~100 moving points × 45m ≈ 4500m, but with stops and artifact, expect 2-8km
	if summary.TotalDistance < 2000 || summary.TotalDistance > 10000 {
		t.Errorf("TotalDistance out of range: %f m", summary.TotalDistance)
	}

	// Elevation: starts at 1050, peaks around 1310, descends
	if summary.Elevation.Max < 1200 {
		t.Errorf("MaxElevation should be >= 1200, got %f", summary.Elevation.Max)
	}
	if summary.Elevation.Min > 1100 {
		t.Errorf("MinElevation should be <= 1100, got %f", summary.Elevation.Min)
	}
	if summary.Elevation.Gain < 100 {
		t.Errorf("ElevationGain should be >= 100, got %f", summary.Elevation.Gain)
	}
	if summary.Elevation.Loss < 50 {
		t.Errorf("ElevationLoss should be >= 50, got %f", summary.Elevation.Loss)
	}

	// Time: 110 minutes
	expectedTime := 109 * time.Minute
	if summary.TotalTime != expectedTime {
		t.Errorf("TotalTime expected %v, got %v", expectedTime, summary.TotalTime)
	}

	// Should detect stops (there are 2 stops in the data)
	if summary.StopCount < 1 {
		t.Errorf("expected at least 1 stop, got %d", summary.StopCount)
	}

	// MovingTime < TotalTime (because of stops)
	if summary.MovingTime >= summary.TotalTime {
		t.Error("MovingTime should be < TotalTime (stops exist)")
	}

	// MaxSpeed should be reasonable after outlier removal (trail preset = 7.0 m/s)
	if summary.Speed.MaxSpeed > PresetMaxSpeed[PresetTrail] {
		t.Errorf("MaxSpeed %f exceeds trail limit %f m/s", summary.Speed.MaxSpeed, PresetMaxSpeed[PresetTrail])
	}

	// The GPS artifact at point 55 should be physically removed by FilterOutliers
	// Without filtering, it would show ~1000m/60s ≈ 16.7 m/s
	if summary.Speed.MaxSpeed > PresetMaxSpeed[PresetTrail] {
		t.Errorf("GPS artifact not filtered: MaxSpeed=%f m/s", summary.Speed.MaxSpeed)
	}

	// FilteredPoints should be > 0 (artifact was removed)
	if summary.FilteredPoints == 0 {
		t.Error("expected at least one filtered point (GPS artifact)")
	}
}

func TestCompute_EmptyPoints(t *testing.T) {
	cfg := DefaultConfig()
	summary, _, err := Compute(nil, 0, cfg)
	if err != nil {
		t.Fatalf("Compute should not error on empty: %v", err)
	}
	if summary.PointCount != 0 {
		t.Errorf("expected 0 points, got %d", summary.PointCount)
	}
	if summary.TotalDistance != 0 {
		t.Errorf("expected 0 distance, got %f", summary.TotalDistance)
	}
}

func TestCompute_MaxSpeedConfigAffectsResult(t *testing.T) {
	// Create points with a high-speed point
	basePoints := []gpx.TrackPoint{
		{Lat: 48.8566, Lon: 2.3522, Time: makeTime(0)},
		{Lat: 48.8580, Lon: 2.3540, Time: makeTime(1)},
		{Lat: 48.8600, Lon: 2.3560, Time: makeTime(2)},
	}

	// With lenient config (cycling preset → 25 m/s threshold)
	cfgLenient := DefaultConfig()
	cfgLenient.MaxReasonableSpeed = PresetMaxSpeed[PresetCycling]
	pointsA := make([]gpx.TrackPoint, len(basePoints))
	copy(pointsA, basePoints)
	s1, _, _ := Compute(pointsA, 1, cfgLenient)

	// With strict max speed limit (1 m/s) — should filter more points
	cfgStrict := DefaultConfig()
	cfgStrict.MaxReasonableSpeed = 1.0
	pointsB := make([]gpx.TrackPoint, len(basePoints))
	copy(pointsB, basePoints)
	s2, _, _ := Compute(pointsB, 1, cfgStrict)

	// Strict filter should remove more points
	if s2.FilteredPoints < s1.FilteredPoints {
		t.Errorf("strict filter should remove >= lenient points: strict=%d, lenient=%d",
			s2.FilteredPoints, s1.FilteredPoints)
	}
}
