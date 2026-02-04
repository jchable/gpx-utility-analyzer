package dem

import (
	"testing"

	"github.com/jchable/gpx-utility-analyzer/internal/gpx"
)

func TestSource_MissingTile(t *testing.T) {
	src := NewSource(t.TempDir()) // empty directory
	_, ok := src.Lookup(48.5, 2.5)
	if ok {
		t.Error("expected ok=false for missing tile")
	}
}

func TestSource_CachesLoaded(t *testing.T) {
	dir := t.TempDir()
	createTestHGT(t, dir, "N48E002.hgt", srtm3Size, func(row, col int) int16 {
		return 500
	})

	src := NewSource(dir)

	ele1, ok1 := src.Lookup(48.5, 2.5)
	ele2, ok2 := src.Lookup(48.5, 2.5)

	if !ok1 || !ok2 {
		t.Fatal("expected both lookups to succeed")
	}
	if ele1 != ele2 {
		t.Errorf("expected same elevation, got %f and %f", ele1, ele2)
	}
	// Verify tile is cached
	if len(src.tiles) != 1 {
		t.Errorf("expected 1 cached tile, got %d", len(src.tiles))
	}
}

func TestCorrectElevations(t *testing.T) {
	dir := t.TempDir()
	createTestHGT(t, dir, "N48E002.hgt", srtm3Size, func(row, col int) int16 {
		return 500
	})

	src := NewSource(dir)
	points := []gpx.TrackPoint{
		{Lat: 48.5, Lon: 2.5, Ele: 999}, // should be corrected to 500
		{Lat: 10.0, Lon: 10.0, Ele: 100}, // no tile available, stays at 100
	}

	CorrectElevations(points, src)

	if points[0].Ele != 500 {
		t.Errorf("expected corrected elevation 500, got %f", points[0].Ele)
	}
	if points[1].Ele != 100 {
		t.Errorf("expected unchanged elevation 100, got %f", points[1].Ele)
	}
}
