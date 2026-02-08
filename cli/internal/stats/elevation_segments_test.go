package stats

import (
	"math"
	"testing"

	"github.com/jchable/gpx-utility-analyzer/cli/internal/gpx"
)

func TestLinearFit_SteadySlope(t *testing.T) {
	profile := []profilePoint{
		{0, 100}, {100, 110}, {200, 120}, {300, 130},
	}
	slope, intercept := linearFit(profile)
	// slope should be 0.1 (10m per 100m), intercept should be 100
	if math.Abs(slope-0.1) > 1e-9 {
		t.Errorf("expected slope 0.1, got %f", slope)
	}
	if math.Abs(intercept-100) > 1e-6 {
		t.Errorf("expected intercept 100, got %f", intercept)
	}
}

func TestLinearFit_Flat(t *testing.T) {
	profile := []profilePoint{
		{0, 500}, {100, 500}, {200, 500},
	}
	slope, _ := linearFit(profile)
	if math.Abs(slope) > 1e-9 {
		t.Errorf("expected slope 0, got %f", slope)
	}
}

func TestRmsResidual_PerfectFit(t *testing.T) {
	profile := []profilePoint{
		{0, 100}, {100, 110}, {200, 120},
	}
	slope, intercept := linearFit(profile)
	rms := rmsResidual(profile, slope, intercept)
	if rms > 1e-9 {
		t.Errorf("expected rms ~0 for perfect fit, got %f", rms)
	}
}

func TestRmsResidual_WithNoise(t *testing.T) {
	profile := []profilePoint{
		{0, 100}, {100, 115}, {200, 120},
	}
	slope, intercept := linearFit(profile)
	rms := rmsResidual(profile, slope, intercept)
	if rms < 1.0 {
		t.Errorf("expected rms > 1 for noisy data, got %f", rms)
	}
}

func TestComputeElevationSegments_SteadyClimb(t *testing.T) {
	// Create points climbing steadily over 1km
	n := 50
	points := make([]gpx.TrackPoint, n)
	for i := range points {
		points[i] = gpx.TrackPoint{
			Lat: 48.0 + float64(i)*0.0002, // ~22m per step
			Lon: 2.0,
			Ele: 100 + float64(i)*2, // 2m per step = 98m total
		}
	}

	r := ComputeElevationSegments(points, 200.0, 2.0)
	// Should capture most of the 98m gain
	if r.Gain < 80 || r.Gain > 110 {
		t.Errorf("expected gain ~98, got %f", r.Gain)
	}
	if r.Loss > 10 {
		t.Errorf("expected loss ~0, got %f", r.Loss)
	}
}

func TestComputeElevationSegments_NoisyFlat(t *testing.T) {
	n := 50
	points := make([]gpx.TrackPoint, n)
	for i := range points {
		noise := 0.0
		if i%2 == 1 {
			noise = 1.0
		}
		points[i] = gpx.TrackPoint{
			Lat: 48.0 + float64(i)*0.0002,
			Lon: 2.0,
			Ele: 500 + noise,
		}
	}

	r := ComputeElevationSegments(points, 200.0, 2.0)
	// With segment-based approach, noise should be mostly eliminated
	if r.Gain > 10 {
		t.Errorf("expected gain < 10 (noise filtered), got %f", r.Gain)
	}
}

func TestComputeElevationSegments_Empty(t *testing.T) {
	r := ComputeElevationSegments(nil, 200.0, 2.0)
	if r.Gain != 0 || r.Loss != 0 {
		t.Errorf("expected zero result for empty points")
	}
}

func TestComputeElevationSegments_MaxMin(t *testing.T) {
	points := []gpx.TrackPoint{
		{Lat: 48.0, Lon: 2.0, Ele: 100},
		{Lat: 48.001, Lon: 2.0, Ele: 200},
		{Lat: 48.002, Lon: 2.0, Ele: 50},
		{Lat: 48.003, Lon: 2.0, Ele: 150},
	}

	r := ComputeElevationSegments(points, 50.0, 2.0)
	if r.Max != 200 {
		t.Errorf("expected max 200, got %f", r.Max)
	}
	if r.Min != 50 {
		t.Errorf("expected min 50, got %f", r.Min)
	}
}
