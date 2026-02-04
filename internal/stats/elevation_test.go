package stats

import (
	"testing"

	"github.com/jchable/gpx-utility-analyzer/internal/gpx"
)

func TestComputeElevation(t *testing.T) {
	t.Run("ascending", func(t *testing.T) {
		points := []gpx.TrackPoint{
			{Ele: 100},
			{Ele: 110},
			{Ele: 120},
			{Ele: 130},
		}
		result := ComputeElevation(points, 2.0)
		if result.Gain != 30 {
			t.Errorf("expected gain 30, got %f", result.Gain)
		}
		if result.Loss != 0 {
			t.Errorf("expected loss 0, got %f", result.Loss)
		}
		if result.Max != 130 {
			t.Errorf("expected max 130, got %f", result.Max)
		}
		if result.Min != 100 {
			t.Errorf("expected min 100, got %f", result.Min)
		}
	})

	t.Run("descending", func(t *testing.T) {
		points := []gpx.TrackPoint{
			{Ele: 130},
			{Ele: 120},
			{Ele: 110},
			{Ele: 100},
		}
		result := ComputeElevation(points, 2.0)
		if result.Gain != 0 {
			t.Errorf("expected gain 0, got %f", result.Gain)
		}
		if result.Loss != 30 {
			t.Errorf("expected loss 30, got %f", result.Loss)
		}
	})

	t.Run("noise filtering", func(t *testing.T) {
		// Small oscillations below threshold should be filtered
		points := []gpx.TrackPoint{
			{Ele: 100},
			{Ele: 101}, // +1, below threshold
			{Ele: 100}, // -1, below threshold
			{Ele: 101}, // +1, below threshold
			{Ele: 100}, // -1, below threshold
		}
		result := ComputeElevation(points, 2.0)
		if result.Gain != 0 {
			t.Errorf("expected 0 gain with noise filtering, got %f", result.Gain)
		}
		if result.Loss != 0 {
			t.Errorf("expected 0 loss with noise filtering, got %f", result.Loss)
		}
	})

	t.Run("mixed with threshold", func(t *testing.T) {
		points := []gpx.TrackPoint{
			{Ele: 100},
			{Ele: 105}, // +5, above threshold
			{Ele: 103}, // -2, at threshold
			{Ele: 110}, // +7, above threshold
		}
		result := ComputeElevation(points, 2.0)
		if result.Gain != 12 {
			t.Errorf("expected gain 12 (5+7), got %f", result.Gain)
		}
		if result.Loss != 2 {
			t.Errorf("expected loss 2, got %f", result.Loss)
		}
	})

	t.Run("empty points", func(t *testing.T) {
		result := ComputeElevation(nil, 2.0)
		if result.Gain != 0 || result.Loss != 0 {
			t.Error("expected zero values for empty points")
		}
	})
}
