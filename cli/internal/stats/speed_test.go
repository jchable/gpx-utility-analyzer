package stats

import (
	"math"
	"testing"
	"time"

	"github.com/jchable/gpx-utility-analyzer/cli/internal/gpx"
)

// --- EnrichPoints ---

func TestEnrichPoints_ComputesDistAndSpeed(t *testing.T) {
	// Two points ~156m apart, 60s interval → ~2.6 m/s
	points := []gpx.TrackPoint{
		{Lat: 48.8566, Lon: 2.3522, Time: makeTime(0)},
		{Lat: 48.8580, Lon: 2.3540, Time: makeTime(1)},
	}
	EnrichPoints(points)

	if points[0].DistFromPrev != 0 {
		t.Errorf("first point DistFromPrev should be 0, got %f", points[0].DistFromPrev)
	}
	if points[0].CalcSpeed != 0 {
		t.Errorf("first point CalcSpeed should be 0, got %f", points[0].CalcSpeed)
	}
	if points[1].DistFromPrev <= 0 {
		t.Errorf("second point DistFromPrev should be > 0, got %f", points[1].DistFromPrev)
	}
	if points[1].CalcSpeed <= 0 {
		t.Errorf("second point CalcSpeed should be > 0, got %f", points[1].CalcSpeed)
	}
	// speed = dist / 60s, should be roughly 2-3 m/s for ~150m distance
	if points[1].CalcSpeed < 1.0 || points[1].CalcSpeed > 5.0 {
		t.Errorf("second point CalcSpeed out of expected range: %f m/s", points[1].CalcSpeed)
	}
}

func TestEnrichPoints_ZeroDt_NoSpeed(t *testing.T) {
	// Two points at the exact same time → speed stays 0
	sameTime := makeTime(0)
	points := []gpx.TrackPoint{
		{Lat: 48.8566, Lon: 2.3522, Time: sameTime},
		{Lat: 48.8580, Lon: 2.3540, Time: sameTime},
	}
	EnrichPoints(points)

	if points[1].CalcSpeed != 0 {
		t.Errorf("expected CalcSpeed=0 for dt=0, got %f", points[1].CalcSpeed)
	}
	// distance should still be computed
	if points[1].DistFromPrev <= 0 {
		t.Errorf("DistFromPrev should be > 0 even with dt=0, got %f", points[1].DistFromPrev)
	}
}

func TestEnrichPoints_SamePosition_ZeroDistance(t *testing.T) {
	points := []gpx.TrackPoint{
		{Lat: 48.8566, Lon: 2.3522, Time: makeTime(0)},
		{Lat: 48.8566, Lon: 2.3522, Time: makeTime(1)},
	}
	EnrichPoints(points)

	if points[1].DistFromPrev != 0 {
		t.Errorf("expected DistFromPrev=0 for same position, got %f", points[1].DistFromPrev)
	}
	if points[1].CalcSpeed != 0 {
		t.Errorf("expected CalcSpeed=0 for same position, got %f", points[1].CalcSpeed)
	}
}

func TestEnrichPoints_MultiplePoints(t *testing.T) {
	points := []gpx.TrackPoint{
		{Lat: 48.8566, Lon: 2.3522, Time: makeTime(0)},
		{Lat: 48.8580, Lon: 2.3540, Time: makeTime(1)},
		{Lat: 48.8600, Lon: 2.3560, Time: makeTime(2)},
		{Lat: 48.8620, Lon: 2.3580, Time: makeTime(3)},
	}
	EnrichPoints(points)

	for i := 1; i < len(points); i++ {
		if points[i].DistFromPrev <= 0 {
			t.Errorf("point %d DistFromPrev should be > 0, got %f", i, points[i].DistFromPrev)
		}
		if points[i].CalcSpeed <= 0 {
			t.Errorf("point %d CalcSpeed should be > 0, got %f", i, points[i].CalcSpeed)
		}
	}
}

func TestEnrichPoints_SinglePoint(t *testing.T) {
	points := []gpx.TrackPoint{
		{Lat: 48.8566, Lon: 2.3522, Time: makeTime(0)},
	}
	EnrichPoints(points)
	// Should not panic and first point should remain zero
	if points[0].DistFromPrev != 0 || points[0].CalcSpeed != 0 {
		t.Error("single point should have zero dist and speed")
	}
}

func TestEnrichPoints_Empty(t *testing.T) {
	var points []gpx.TrackPoint
	EnrichPoints(points) // Should not panic
}

// --- ComputeSpeed ---

func TestComputeSpeed_Normal(t *testing.T) {
	// 10km in 1h total, 50min moving
	dist := 10000.0 // meters
	totalTime := time.Hour
	movingTime := 50 * time.Minute

	result := ComputeSpeed(dist, totalTime, movingTime)

	// AvgSpeed = 10000/3600 ≈ 2.78 m/s
	if math.Abs(result.AvgSpeed-2.778) > 0.01 {
		t.Errorf("AvgSpeed expected ~2.78, got %f", result.AvgSpeed)
	}
	// AvgMovingSpeed = 10000/3000 ≈ 3.33 m/s
	if math.Abs(result.AvgMovingSpeed-3.333) > 0.01 {
		t.Errorf("AvgMovingSpeed expected ~3.33, got %f", result.AvgMovingSpeed)
	}
	// Moving speed should be higher than total speed
	if result.AvgMovingSpeed <= result.AvgSpeed {
		t.Error("AvgMovingSpeed should be > AvgSpeed")
	}
	// Pace
	if result.AvgPace <= 0 {
		t.Error("AvgPace should be > 0")
	}
	if result.AvgMovingPace <= 0 {
		t.Error("AvgMovingPace should be > 0")
	}
	// Moving pace should be faster (smaller) than total pace
	if result.AvgMovingPace >= result.AvgPace {
		t.Error("AvgMovingPace should be < AvgPace")
	}
}

func TestComputeSpeed_ZeroTime(t *testing.T) {
	result := ComputeSpeed(10000, 0, 0)
	if result.AvgSpeed != 0 {
		t.Errorf("expected AvgSpeed=0 for zero time, got %f", result.AvgSpeed)
	}
	if result.AvgMovingSpeed != 0 {
		t.Errorf("expected AvgMovingSpeed=0 for zero time, got %f", result.AvgMovingSpeed)
	}
}

func TestComputeSpeed_ZeroDistance(t *testing.T) {
	result := ComputeSpeed(0, time.Hour, 50*time.Minute)
	if result.AvgSpeed != 0 {
		t.Errorf("expected AvgSpeed=0 for zero distance, got %f", result.AvgSpeed)
	}
	if result.AvgPace != 0 {
		t.Errorf("expected AvgPace=0 for zero distance, got %v", result.AvgPace)
	}
}

// --- MaxSpeedFromPoints ---

func TestMaxSpeedFromPoints_ReturnsMax(t *testing.T) {
	// After FilterOutliers, points should only have reasonable speeds
	points := []gpx.TrackPoint{
		{CalcSpeed: 2.0},
		{CalcSpeed: 3.5},
		{CalcSpeed: 6.5},
		{CalcSpeed: 4.0},
		{CalcSpeed: 1.0},
	}
	max := MaxSpeedFromPoints(points)
	if max != 6.5 {
		t.Errorf("expected max=6.5, got %f", max)
	}
}

func TestMaxSpeedFromPoints_Empty(t *testing.T) {
	max := MaxSpeedFromPoints(nil)
	if max != 0 {
		t.Errorf("expected max=0 for empty points, got %f", max)
	}
}

func TestMaxSpeedFromPoints_SinglePoint(t *testing.T) {
	points := []gpx.TrackPoint{
		{CalcSpeed: 5.0},
	}
	max := MaxSpeedFromPoints(points)
	if max != 5.0 {
		t.Errorf("expected max=5.0, got %f", max)
	}
}

func TestMaxSpeedFromPoints_AllZero(t *testing.T) {
	points := []gpx.TrackPoint{
		{CalcSpeed: 0},
		{CalcSpeed: 0},
	}
	max := MaxSpeedFromPoints(points)
	if max != 0 {
		t.Errorf("expected max=0 for all-zero speeds, got %f", max)
	}
}

// --- PresetMaxSpeed ---

func TestPresetMaxSpeed_AllPresetsHaveEntry(t *testing.T) {
	for _, name := range []string{PresetHiking, PresetTrail, PresetCycling} {
		speed, ok := PresetMaxSpeed[name]
		if !ok {
			t.Errorf("PresetMaxSpeed missing entry for %q", name)
		}
		if speed <= 0 {
			t.Errorf("PresetMaxSpeed[%q] should be > 0, got %f", name, speed)
		}
	}
}

func TestPresetMaxSpeed_HikingLessThanTrail(t *testing.T) {
	if PresetMaxSpeed[PresetHiking] >= PresetMaxSpeed[PresetTrail] {
		t.Errorf("hiking max speed (%f) should be < trail (%f)",
			PresetMaxSpeed[PresetHiking], PresetMaxSpeed[PresetTrail])
	}
}

func TestPresetMaxSpeed_TrailLessThanCycling(t *testing.T) {
	if PresetMaxSpeed[PresetTrail] >= PresetMaxSpeed[PresetCycling] {
		t.Errorf("trail max speed (%f) should be < cycling (%f)",
			PresetMaxSpeed[PresetTrail], PresetMaxSpeed[PresetCycling])
	}
}

func TestPresetMaxSpeed_ReasonableValues(t *testing.T) {
	// Hiking: should be < 15 km/h (~4.2 m/s)
	if PresetMaxSpeed[PresetHiking] > 5.0 {
		t.Errorf("hiking max speed seems too high: %f m/s", PresetMaxSpeed[PresetHiking])
	}
	// Trail: should be < 30 km/h (~8.3 m/s)
	if PresetMaxSpeed[PresetTrail] > 8.5 {
		t.Errorf("trail max speed seems too high: %f m/s", PresetMaxSpeed[PresetTrail])
	}
	// Cycling: should be < 100 km/h (~27.8 m/s)
	if PresetMaxSpeed[PresetCycling] > 28.0 {
		t.Errorf("cycling max speed seems too high: %f m/s", PresetMaxSpeed[PresetCycling])
	}
}

// --- EnrichPoints gap-awareness ---

func TestEnrichPoints_SkipsLargeTimeGap(t *testing.T) {
	// Two points 15 minutes apart — should be treated as a gap (> 10-min threshold)
	points := []gpx.TrackPoint{
		{Lat: 48.0, Lon: 2.0, Time: makeTime(0)},
		{Lat: 49.0, Lon: 3.0, Time: makeTime(15)}, // 15 minutes, >10min gap
	}
	EnrichPoints(points)

	if points[1].CalcSpeed != 0 {
		t.Errorf("expected CalcSpeed=0 across gap, got %f", points[1].CalcSpeed)
	}
	if points[1].DistFromPrev != 0 {
		t.Errorf("expected DistFromPrev=0 across gap, got %f", points[1].DistFromPrev)
	}
}

func TestEnrichPoints_NormalIntervalStillWorks(t *testing.T) {
	// Two points 1 minute apart — should compute normally
	points := []gpx.TrackPoint{
		{Lat: 48.8566, Lon: 2.3522, Time: makeTime(0)},
		{Lat: 48.8580, Lon: 2.3540, Time: makeTime(1)},
	}
	EnrichPoints(points)

	if points[1].CalcSpeed <= 0 {
		t.Errorf("expected CalcSpeed>0 for normal interval, got %f", points[1].CalcSpeed)
	}
	if points[1].DistFromPrev <= 0 {
		t.Errorf("expected DistFromPrev>0 for normal interval, got %f", points[1].DistFromPrev)
	}
}

// --- ClampSpeeds ---

func TestClampSpeeds_ClampsExcessiveSpeed(t *testing.T) {
	points := []gpx.TrackPoint{
		{CalcSpeed: 0, DistFromPrev: 0},
		{CalcSpeed: 3.0, DistFromPrev: 180},  // normal
		{CalcSpeed: 50.0, DistFromPrev: 3000}, // excessive
		{CalcSpeed: 2.0, DistFromPrev: 120},   // normal
	}
	clamped := ClampSpeeds(points, 7.0)

	if clamped != 1 {
		t.Errorf("expected 1 clamped, got %d", clamped)
	}
	if points[2].CalcSpeed != 0 {
		t.Errorf("expected CalcSpeed=0 for clamped point, got %f", points[2].CalcSpeed)
	}
	if points[2].DistFromPrev != 0 {
		t.Errorf("expected DistFromPrev=0 for clamped point, got %f", points[2].DistFromPrev)
	}
	// Other points should be unchanged
	if points[1].CalcSpeed != 3.0 {
		t.Errorf("normal point should be unchanged, got %f", points[1].CalcSpeed)
	}
	if points[3].CalcSpeed != 2.0 {
		t.Errorf("normal point should be unchanged, got %f", points[3].CalcSpeed)
	}
}

func TestClampSpeeds_NoClampNeeded(t *testing.T) {
	points := []gpx.TrackPoint{
		{CalcSpeed: 0},
		{CalcSpeed: 3.0, DistFromPrev: 180},
		{CalcSpeed: 5.0, DistFromPrev: 300},
	}
	clamped := ClampSpeeds(points, 7.0)

	if clamped != 0 {
		t.Errorf("expected 0 clamped, got %d", clamped)
	}
}

func TestClampSpeeds_DisabledWhenZero(t *testing.T) {
	points := []gpx.TrackPoint{
		{CalcSpeed: 0},
		{CalcSpeed: 999.0, DistFromPrev: 60000},
	}
	clamped := ClampSpeeds(points, 0)

	if clamped != 0 {
		t.Errorf("expected 0 clamped when disabled, got %d", clamped)
	}
	if points[1].CalcSpeed != 999.0 {
		t.Errorf("point should be unchanged when disabled, got %f", points[1].CalcSpeed)
	}
}
