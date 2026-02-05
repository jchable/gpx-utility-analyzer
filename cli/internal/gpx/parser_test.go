package gpx

import (
	"strings"
	"testing"
)

func TestParseSmallGPX(t *testing.T) {
	g, err := ParseFile("../../testdata/small.gpx")
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}

	if len(g.Tracks) != 1 {
		t.Fatalf("expected 1 track, got %d", len(g.Tracks))
	}
	if g.Tracks[0].Name != "Test Track" {
		t.Errorf("expected track name 'Test Track', got %q", g.Tracks[0].Name)
	}
	if g.PointCount() != 5 {
		t.Errorf("expected 5 points, got %d", g.PointCount())
	}
	if g.SegmentCount() != 1 {
		t.Errorf("expected 1 segment, got %d", g.SegmentCount())
	}
}

func TestParseTwoSegments(t *testing.T) {
	g, err := ParseFile("../../testdata/two-segments.gpx")
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}

	if g.SegmentCount() != 2 {
		t.Errorf("expected 2 segments, got %d", g.SegmentCount())
	}
	if g.PointCount() != 4 {
		t.Errorf("expected 4 points, got %d", g.PointCount())
	}
}

func TestParseEmptyGPX(t *testing.T) {
	data := `<?xml version="1.0"?><gpx version="1.0"><trk><trkseg></trkseg></trk></gpx>`
	_, err := Parse(strings.NewReader(data))
	if err == nil {
		t.Error("expected error for empty GPX, got nil")
	}
}

func TestAllPoints(t *testing.T) {
	g, err := ParseFile("../../testdata/small.gpx")
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}

	points, err := g.AllPoints()
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if len(points) != 5 {
		t.Errorf("expected 5 trackpoints, got %d", len(points))
	}

	// Check first point values
	if points[0].Lat != 48.8566 {
		t.Errorf("expected lat 48.8566, got %f", points[0].Lat)
	}
	if points[0].Ele != 35.0 {
		t.Errorf("expected ele 35.0, got %f", points[0].Ele)
	}
	if points[0].Time.IsZero() {
		t.Error("expected non-zero time")
	}
}
