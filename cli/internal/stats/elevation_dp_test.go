package stats

import (
	"math"
	"testing"

	"github.com/jchable/gpx-utility-analyzer/internal/gpx"
)

func TestPerpendicularDistance_PointOnLine(t *testing.T) {
	a := profilePoint{cumDist: 0, ele: 100}
	b := profilePoint{cumDist: 100, ele: 200}
	p := profilePoint{cumDist: 50, ele: 150}

	d := perpendicularDistance(p, a, b)
	if d > 1e-9 {
		t.Errorf("expected 0, got %f", d)
	}
}

func TestPerpendicularDistance_PointAboveLine(t *testing.T) {
	a := profilePoint{cumDist: 0, ele: 100}
	b := profilePoint{cumDist: 100, ele: 100}
	p := profilePoint{cumDist: 50, ele: 110}

	d := perpendicularDistance(p, a, b)
	if math.Abs(d-10) > 1e-9 {
		t.Errorf("expected 10, got %f", d)
	}
}

func TestDouglasPeucker_SteadyClimb(t *testing.T) {
	// A steady climb: all points lie on the line from first to last
	profile := []profilePoint{
		{0, 100}, {100, 110}, {200, 120}, {300, 130}, {400, 140},
	}
	indices := douglasPeucker(profile, 3.0)
	// Should keep only endpoints since all points are on the line
	if len(indices) != 2 {
		t.Errorf("expected 2 retained points, got %d: %v", len(indices), indices)
	}
}

func TestDouglasPeucker_VProfile(t *testing.T) {
	// V-shaped profile: must retain the valley point
	profile := []profilePoint{
		{0, 100}, {50, 80}, {100, 60}, {150, 80}, {200, 100},
	}
	indices := douglasPeucker(profile, 3.0)
	// The bottom of the V (index 2, ele=60) must be retained
	found := false
	for _, idx := range indices {
		if idx == 2 {
			found = true
			break
		}
	}
	if !found {
		t.Errorf("expected valley point (index 2) to be retained, got %v", indices)
	}
}

func TestComputeElevationDP_SteadyClimb(t *testing.T) {
	// 5 points climbing 10m each = 40m total gain, 0 loss
	points := make([]gpx.TrackPoint, 5)
	for i := range points {
		points[i] = gpx.TrackPoint{
			Lat: 48.0 + float64(i)*0.001,
			Lon: 2.0,
			Ele: 100 + float64(i)*10,
		}
	}

	r := ComputeElevationDP(points, 3.0)
	if math.Abs(r.Gain-40) > 1.0 {
		t.Errorf("expected gain ~40, got %f", r.Gain)
	}
	if r.Loss > 1.0 {
		t.Errorf("expected loss ~0, got %f", r.Loss)
	}
	if r.Max != 140 {
		t.Errorf("expected max 140, got %f", r.Max)
	}
	if r.Min != 100 {
		t.Errorf("expected min 100, got %f", r.Min)
	}
}

func TestComputeElevationDP_NoisyFlat(t *testing.T) {
	// Flat terrain with small oscillations (noise)
	points := make([]gpx.TrackPoint, 20)
	for i := range points {
		noise := 0.0
		if i%2 == 1 {
			noise = 1.5 // ±1.5m noise
		}
		points[i] = gpx.TrackPoint{
			Lat: 48.0 + float64(i)*0.001,
			Lon: 2.0,
			Ele: 500 + noise,
		}
	}

	r := ComputeElevationDP(points, 3.0)
	// With epsilon=3, the noise should be filtered out
	if r.Gain > 5.0 {
		t.Errorf("expected gain < 5 (noise filtered), got %f", r.Gain)
	}
	if r.Loss > 5.0 {
		t.Errorf("expected loss < 5 (noise filtered), got %f", r.Loss)
	}
}

func TestComputeElevationDP_EmptyPoints(t *testing.T) {
	r := ComputeElevationDP(nil, 3.0)
	if r.Gain != 0 || r.Loss != 0 {
		t.Errorf("expected zero result for empty points, got gain=%f loss=%f", r.Gain, r.Loss)
	}
}

func TestComputeElevationDP_SinglePoint(t *testing.T) {
	points := []gpx.TrackPoint{{Lat: 48.0, Lon: 2.0, Ele: 500}}
	r := ComputeElevationDP(points, 3.0)
	if r.Max != 500 || r.Min != 500 {
		t.Errorf("expected max=min=500, got max=%f min=%f", r.Max, r.Min)
	}
}
