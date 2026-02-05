package dem

import (
	"encoding/binary"
	"os"
	"path/filepath"
	"testing"
)

func TestTileKey(t *testing.T) {
	tests := []struct {
		lat, lon float64
		expected string
	}{
		{48.5, 2.3, "N48E002"},
		{48.5, -2.3, "N48W003"},
		{-34.1, 18.9, "S35E018"},
		{0.5, -0.5, "N00W001"},
		{49.0, -120.5, "N49W121"},
	}

	for _, tt := range tests {
		got := TileKey(tt.lat, tt.lon)
		if got != tt.expected {
			t.Errorf("TileKey(%f, %f) = %q, expected %q", tt.lat, tt.lon, got, tt.expected)
		}
	}
}

func TestParseFilename(t *testing.T) {
	lat, lon, err := parseFilename("N48W003.hgt")
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if lat != 48 || lon != -3 {
		t.Errorf("expected (48, -3), got (%d, %d)", lat, lon)
	}

	lat, lon, err = parseFilename("S35E018.hgt")
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if lat != -35 || lon != 18 {
		t.Errorf("expected (-35, 18), got (%d, %d)", lat, lon)
	}
}

// createTestHGT creates a small SRTM3-format HGT file with known values.
func createTestHGT(t *testing.T, dir, name string, gridSize int, fillFn func(row, col int) int16) string {
	t.Helper()
	path := filepath.Join(dir, name)
	f, err := os.Create(path)
	if err != nil {
		t.Fatalf("creating test HGT: %v", err)
	}
	defer f.Close()

	data := make([]int16, gridSize*gridSize)
	for r := 0; r < gridSize; r++ {
		for c := 0; c < gridSize; c++ {
			data[r*gridSize+c] = fillFn(r, c)
		}
	}
	if err := binary.Write(f, binary.BigEndian, data); err != nil {
		t.Fatalf("writing test HGT: %v", err)
	}
	return path
}

func TestLoadTile_SRTM3(t *testing.T) {
	dir := t.TempDir()
	path := createTestHGT(t, dir, "N48E002.hgt", srtm3Size, func(row, col int) int16 {
		return 500 // flat terrain at 500m
	})

	tile, err := LoadTile(path)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if tile.GridSize != srtm3Size {
		t.Errorf("expected grid size %d, got %d", srtm3Size, tile.GridSize)
	}
	if tile.LatOrigin != 48 || tile.LonOrigin != 2 {
		t.Errorf("expected origin (48, 2), got (%d, %d)", tile.LatOrigin, tile.LonOrigin)
	}
}

func TestElevation_Flat(t *testing.T) {
	dir := t.TempDir()
	createTestHGT(t, dir, "N48E002.hgt", srtm3Size, func(row, col int) int16 {
		return 500
	})

	tile, _ := LoadTile(filepath.Join(dir, "N48E002.hgt"))
	ele, ok := tile.Elevation(48.5, 2.5)
	if !ok {
		t.Fatal("expected ok=true")
	}
	if ele != 500 {
		t.Errorf("expected 500, got %f", ele)
	}
}

func TestElevation_Bilinear(t *testing.T) {
	dir := t.TempDir()
	// Create a tile with a gradient: elevation = row index
	createTestHGT(t, dir, "N48E002.hgt", srtm3Size, func(row, col int) int16 {
		return int16(row) // 0 at north, 1200 at south
	})

	tile, _ := LoadTile(filepath.Join(dir, "N48E002.hgt"))

	// Point at exact center of tile (lat=48.5, lon=2.5)
	ele, ok := tile.Elevation(48.5, 2.5)
	if !ok {
		t.Fatal("expected ok=true")
	}
	// At lat 48.5, row = 1200 * (49 - 48.5) = 600
	expected := 600.0
	if ele < expected-1 || ele > expected+1 {
		t.Errorf("expected ~%f, got %f", expected, ele)
	}
}

func TestElevation_Void(t *testing.T) {
	dir := t.TempDir()
	createTestHGT(t, dir, "N48E002.hgt", srtm3Size, func(row, col int) int16 {
		return voidValue
	})

	tile, _ := LoadTile(filepath.Join(dir, "N48E002.hgt"))
	_, ok := tile.Elevation(48.5, 2.5)
	if ok {
		t.Error("expected ok=false for void tile")
	}
}

func TestElevation_ExactGridPoint(t *testing.T) {
	dir := t.TempDir()
	createTestHGT(t, dir, "N48E002.hgt", srtm3Size, func(row, col int) int16 {
		if row == 0 && col == 0 {
			return 1000 // NW corner = (49.0, 2.0)
		}
		return 500
	})

	tile, _ := LoadTile(filepath.Join(dir, "N48E002.hgt"))
	// NW corner: lat=49.0, lon=2.0 → row=0, col=0
	ele, ok := tile.Elevation(49.0, 2.0)
	if !ok {
		t.Fatal("expected ok=true")
	}
	if ele != 1000 {
		t.Errorf("expected 1000, got %f", ele)
	}
}
