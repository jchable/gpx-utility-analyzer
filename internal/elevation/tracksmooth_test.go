package elevation

import (
	"math"
	"testing"

	"github.com/jchable/gpx-utility-analyzer/internal/gpx"
)

func TestSmoothTrack_None(t *testing.T) {
	points := []gpx.TrackPoint{
		{Lat: 48.0, Lon: 2.0},
		{Lat: 48.1, Lon: 2.1},
		{Lat: 48.2, Lon: 2.2},
	}
	orig := make([]gpx.TrackPoint, len(points))
	copy(orig, points)

	SmoothTrack(points, TrackSmoothNone)

	for i := range points {
		if points[i].Lat != orig[i].Lat || points[i].Lon != orig[i].Lon {
			t.Errorf("point %d changed with TrackSmoothNone", i)
		}
	}
}

func TestSmoothTrack_ReducesNoise(t *testing.T) {
	// Create a straight line with noise
	n := 20
	points := make([]gpx.TrackPoint, n)
	for i := range points {
		noise := 0.0
		if i%2 == 1 {
			noise = 0.0001 // ~11m noise
		}
		points[i] = gpx.TrackPoint{
			Lat: 48.0 + float64(i)*0.001,
			Lon: 2.0 + noise,
		}
	}

	// Compute variance of Lon before smoothing
	meanLon := 0.0
	for _, p := range points {
		meanLon += p.Lon
	}
	meanLon /= float64(n)
	varBefore := 0.0
	for _, p := range points {
		d := p.Lon - meanLon
		varBefore += d * d
	}

	SmoothTrack(points, TrackSmoothMedium)

	// Compute variance of Lon after smoothing
	meanLon = 0.0
	for _, p := range points {
		meanLon += p.Lon
	}
	meanLon /= float64(n)
	varAfter := 0.0
	for _, p := range points {
		d := p.Lon - meanLon
		varAfter += d * d
	}

	// Noise variance should be reduced
	if varAfter >= varBefore {
		t.Errorf("expected variance to decrease: before=%f after=%f", varBefore, varAfter)
	}
}

func TestSmoothTrack_PreservesElevation(t *testing.T) {
	points := []gpx.TrackPoint{
		{Lat: 48.0, Lon: 2.0, Ele: 100},
		{Lat: 48.1, Lon: 2.1, Ele: 200},
		{Lat: 48.2, Lon: 2.2, Ele: 300},
	}

	SmoothTrack(points, TrackSmoothMedium)

	// Elevation should NOT be modified
	if points[0].Ele != 100 || points[1].Ele != 200 || points[2].Ele != 300 {
		t.Error("track smoothing should not modify elevation")
	}
}

func TestValidTrackSmoothingLevel(t *testing.T) {
	valid := []string{"none", "light", "medium", "heavy"}
	for _, v := range valid {
		if !ValidTrackSmoothingLevel(v) {
			t.Errorf("expected %q to be valid", v)
		}
	}
	if ValidTrackSmoothingLevel("invalid") {
		t.Error("expected 'invalid' to be invalid")
	}
}

func TestSmoothTrack_AllLevels(t *testing.T) {
	levels := []TrackSmoothingLevel{TrackSmoothLight, TrackSmoothMedium, TrackSmoothHeavy}
	for _, level := range levels {
		points := []gpx.TrackPoint{
			{Lat: 48.0, Lon: 2.0},
			{Lat: 48.001, Lon: 2.001},
			{Lat: 48.002, Lon: 2.002},
			{Lat: 48.003, Lon: 2.003},
			{Lat: 48.004, Lon: 2.004},
		}

		SmoothTrack(points, level)

		// Just verify it doesn't panic and values are reasonable
		for i, p := range points {
			if math.IsNaN(p.Lat) || math.IsNaN(p.Lon) {
				t.Errorf("level %s: point %d has NaN", level, i)
			}
		}
	}
}
