package dem

import (
	"os"
	"path/filepath"
	"testing"

	"github.com/jchable/gpx-utility-analyzer/cli/internal/gpx"
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
		{Lat: 48.5, Lon: 2.5, Ele: 999},  // should be corrected to 500
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

func TestTileCachePath(t *testing.T) {
	got := TileCachePath("/cache", "N48E002")
	expected := filepath.Join("/cache", "N48", "N48E002.hgt")
	if got != expected {
		t.Errorf("TileCachePath = %q, expected %q", got, expected)
	}

	got = TileCachePath("/cache", "S35W120")
	expected = filepath.Join("/cache", "S35", "S35W120.hgt")
	if got != expected {
		t.Errorf("TileCachePath = %q, expected %q", got, expected)
	}
}

func TestSource_HierarchicalCache(t *testing.T) {
	cacheDir := t.TempDir()
	// Create tile in hierarchical path: cacheDir/N48/N48E002.hgt
	subDir := filepath.Join(cacheDir, "N48")
	os.MkdirAll(subDir, 0o755)
	createTestHGT(t, subDir, "N48E002.hgt", srtm3Size, func(row, col int) int16 {
		return 750
	})

	src := NewSourceWithCache("", cacheDir, false)
	ele, ok := src.Lookup(48.5, 2.5)
	if !ok {
		t.Fatal("expected ok=true from hierarchical cache")
	}
	if ele != 750 {
		t.Errorf("expected 750, got %f", ele)
	}
}

func TestSource_FlatCacheBackwardCompat(t *testing.T) {
	cacheDir := t.TempDir()
	// Create tile in flat path (old format): cacheDir/N48E002.hgt
	createTestHGT(t, cacheDir, "N48E002.hgt", srtm3Size, func(row, col int) int16 {
		return 600
	})

	src := NewSourceWithCache("", cacheDir, false)
	ele, ok := src.Lookup(48.5, 2.5)
	if !ok {
		t.Fatal("expected ok=true from flat cache (backward compat)")
	}
	if ele != 600 {
		t.Errorf("expected 600, got %f", ele)
	}
}

func TestValidateTile_Valid(t *testing.T) {
	dir := t.TempDir()
	createTestHGT(t, dir, "N48E002.hgt", srtm3Size, func(row, col int) int16 {
		return 500
	})
	tile, err := LoadTile(filepath.Join(dir, "N48E002.hgt"))
	if err != nil {
		t.Fatal(err)
	}
	if !ValidateTile(tile) {
		t.Error("expected valid tile")
	}
}

func TestValidateTile_AllVoid(t *testing.T) {
	dir := t.TempDir()
	createTestHGT(t, dir, "N48E002.hgt", srtm3Size, func(row, col int) int16 {
		return voidValue
	})
	tile, err := LoadTile(filepath.Join(dir, "N48E002.hgt"))
	if err != nil {
		t.Fatal(err)
	}
	if ValidateTile(tile) {
		t.Error("expected invalid tile (all void)")
	}
}

func TestValidateTile_Nil(t *testing.T) {
	if ValidateTile(nil) {
		t.Error("expected false for nil tile")
	}
}

func TestSource_SkipValidation(t *testing.T) {
	dir := t.TempDir()
	createTestHGT(t, dir, "N48E002.hgt", srtm3Size, func(row, col int) int16 {
		return voidValue
	})

	// With validation: tile should be rejected
	src1 := NewSource(dir)
	_, ok := src1.Lookup(48.5, 2.5)
	if ok {
		t.Error("expected ok=false with validation for all-void tile")
	}

	// With skip validation: tile should be loaded (but elevation lookup fails due to void)
	src2 := NewSource(dir).WithSkipValidation(true)
	_, ok = src2.Lookup(48.5, 2.5)
	// Tile is loaded but elevation is void, so ok=false
	if ok {
		t.Error("expected ok=false for void elevation even with skip-validation")
	}
	// But the tile should be in the cache (not nil)
	key := TileKey(48.5, 2.5)
	if src2.tiles[key] == nil {
		t.Error("expected tile to be loaded (skip-validation) even if all void")
	}
}

func TestTileMemoryBytes(t *testing.T) {
	if TileMemoryBytes(srtm3Size) != expectedSRTM3Size {
		t.Errorf("expected %d, got %d", expectedSRTM3Size, TileMemoryBytes(srtm3Size))
	}
	if TileMemoryBytes(srtm1Size) != expectedSRTM1Size {
		t.Errorf("expected %d, got %d", expectedSRTM1Size, TileMemoryBytes(srtm1Size))
	}
}

func TestSource_CrossTileElevation(t *testing.T) {
	dir := t.TempDir()

	// Create N48E002 tile with elevation=500 everywhere
	createTestHGT(t, dir, "N48E002.hgt", srtm3Size, func(row, col int) int16 {
		return 500
	})
	// Create N48E003 tile (east neighbor) with elevation=600 everywhere
	createTestHGT(t, dir, "N48E003.hgt", srtm3Size, func(row, col int) int16 {
		return 600
	})

	src := NewSource(dir)

	// Point at the east boundary of N48E002 (lon very close to 3.0)
	// lon=2.9999 is still in N48E002 but interpolation needs N48E003
	// At lon exactly 3.0, floor(3.0) = 3 → TileKey gives N48E003
	// At lon 2.9999, floor = 2 → TileKey gives N48E002, col ≈ GridSize-1
	ele, ok := src.Elevation(48.5, 2.99999)
	if !ok {
		t.Fatal("expected ok=true for cross-tile boundary point")
	}
	// Should be interpolated between 500 and 600
	if ele < 499 || ele > 601 {
		t.Errorf("expected elevation between 500 and 600, got %f", ele)
	}
}

func TestSource_ElevationInterior(t *testing.T) {
	dir := t.TempDir()
	createTestHGT(t, dir, "N48E002.hgt", srtm3Size, func(row, col int) int16 {
		return 500
	})

	src := NewSource(dir)

	// Interior point should work normally
	ele, ok := src.Elevation(48.5, 2.5)
	if !ok {
		t.Fatal("expected ok=true")
	}
	if ele != 500 {
		t.Errorf("expected 500, got %f", ele)
	}
}
