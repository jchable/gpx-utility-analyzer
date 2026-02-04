package stats

import (
	"math"
	"testing"
)

func TestHaversine(t *testing.T) {
	tests := []struct {
		name     string
		lat1     float64
		lon1     float64
		lat2     float64
		lon2     float64
		expected float64 // meters, approximate
		tolerance float64
	}{
		{
			name:     "Paris to London",
			lat1:     48.8566, lon1: 2.3522,
			lat2:     51.5074, lon2: -0.1278,
			expected: 343_550, // ~343.5 km
			tolerance: 1000,   // 1 km tolerance
		},
		{
			name:     "Same point",
			lat1:     48.8566, lon1: 2.3522,
			lat2:     48.8566, lon2: 2.3522,
			expected: 0,
			tolerance: 0.01,
		},
		{
			name:     "Short distance (~200m)",
			lat1:     48.8566, lon1: 2.3522,
			lat2:     48.8580, lon2: 2.3540,
			expected: 200,
			tolerance: 20,
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			got := Haversine(tt.lat1, tt.lon1, tt.lat2, tt.lon2)
			if math.Abs(got-tt.expected) > tt.tolerance {
				t.Errorf("Haversine() = %f, expected %f (±%f)", got, tt.expected, tt.tolerance)
			}
		})
	}
}

func TestDistance3D(t *testing.T) {
	// Same lat/lon but 100m elevation difference
	d2d := Haversine(48.8566, 2.3522, 48.8580, 2.3540)
	d3d := Distance3D(48.8566, 2.3522, 0, 48.8580, 2.3540, 100)

	if d3d <= d2d {
		t.Errorf("3D distance (%f) should be greater than 2D distance (%f)", d3d, d2d)
	}

	// With zero elevation change, 3D should equal 2D
	d3dFlat := Distance3D(48.8566, 2.3522, 50, 48.8580, 2.3540, 50)
	if math.Abs(d3dFlat-d2d) > 0.01 {
		t.Errorf("3D flat distance (%f) should equal 2D distance (%f)", d3dFlat, d2d)
	}
}
