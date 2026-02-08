package elevation

import (
	"math"
	"testing"

	"github.com/jchable/gpx-utility-analyzer/cli/internal/gpx"
)

func TestMedianFilter_RemovesSpike(t *testing.T) {
	data := []float64{100, 100, 7583, 100, 100}
	result := medianFilter(data, 5)
	if result[2] != 100 {
		t.Errorf("expected spike at index 2 to be removed (100), got %f", result[2])
	}
}

func TestMedianFilter_PreservesMonotonic(t *testing.T) {
	data := []float64{100, 110, 120, 130, 140, 150, 160}
	result := medianFilter(data, 3)
	for i := 1; i < len(result); i++ {
		if result[i] < result[i-1] {
			t.Errorf("monotonic sequence broken at index %d: %f < %f", i, result[i], result[i-1])
		}
	}
}

func TestMedianFilter_SinglePoint(t *testing.T) {
	data := []float64{42}
	result := medianFilter(data, 5)
	if result[0] != 42 {
		t.Errorf("expected 42, got %f", result[0])
	}
}

func TestMedianFilter_WindowLargerThanData(t *testing.T) {
	data := []float64{100, 200, 150}
	result := medianFilter(data, 7)
	if len(result) != 3 {
		t.Fatalf("expected 3 results, got %d", len(result))
	}
	// Middle value should be the median of all 3: 150
	if result[1] != 150 {
		t.Errorf("expected median 150 at index 1, got %f", result[1])
	}
}

func TestMovingAverage_Smoothing(t *testing.T) {
	data := []float64{100, 102, 98, 103, 97}
	result := movingAverage(data, 3)
	mean := 100.0
	for _, v := range result {
		if math.Abs(v-mean) > 5 {
			t.Errorf("expected values close to %f, got %f", mean, v)
		}
	}
}

func TestMovingAverage_Constant(t *testing.T) {
	data := []float64{50, 50, 50, 50, 50}
	result := movingAverage(data, 5)
	for i, v := range result {
		if v != 50 {
			t.Errorf("index %d: expected 50, got %f", i, v)
		}
	}
}

func TestSmoothElevations_None(t *testing.T) {
	points := []gpx.TrackPoint{
		{Ele: 100},
		{Ele: 7583},
		{Ele: 100},
	}
	original := make([]float64, len(points))
	for i, p := range points {
		original[i] = p.Ele
	}

	SmoothElevations(points, SmoothNone)

	for i, p := range points {
		if p.Ele != original[i] {
			t.Errorf("SmoothNone modified point %d: expected %f, got %f", i, original[i], p.Ele)
		}
	}
}

func TestSmoothElevations_RemovesSpike(t *testing.T) {
	points := []gpx.TrackPoint{
		{Ele: 100}, {Ele: 100}, {Ele: 7583}, {Ele: 100}, {Ele: 100},
	}
	SmoothElevations(points, SmoothMedium)

	if points[2].Ele > 200 {
		t.Errorf("spike should be removed, got %f", points[2].Ele)
	}
}

func TestValidLevel(t *testing.T) {
	if !ValidLevel("none") {
		t.Error("'none' should be valid")
	}
	if !ValidLevel("medium") {
		t.Error("'medium' should be valid")
	}
	if ValidLevel("invalid") {
		t.Error("'invalid' should not be valid")
	}
}
