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

func TestSource_CorrectElevation(t *testing.T) {
	dir := t.TempDir()
	createTestHGT(t, dir, "N48E002.hgt", srtm3Size, func(row, col int) int16 {
		return 500
	})

	src := NewSource(dir)
	points := []gpx.TrackPoint{
		{Lat: 48.5, Lon: 2.5, Ele: 999},  // should be corrected to 500
		{Lat: 10.0, Lon: 10.0, Ele: 100}, // no tile available, stays at 100
	}

	// Correct elevations via the Elevation interface
	for i := range points {
		if ele, ok := src.Elevation(points[i].Lat, points[i].Lon); ok {
			points[i].Ele = ele
		}
	}

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
	ele, ok := src.Elevation(48.5, 2.99999)
	if !ok {
		t.Fatal("expected ok=true for cross-tile boundary point")
	}
	// At lon≈2.99999, col fraction is ~0.999988. Interpolation between 500 and 600
	// should give a value very close to 500 (mostly from the primary tile).
	if ele < 500 || ele > 510 {
		t.Errorf("expected elevation close to 500 (east boundary), got %f", ele)
	}
}

func TestSource_CrossTileElevation_South(t *testing.T) {
	dir := t.TempDir()

	// Create N48E002 (lat origin=48) with elevation=500
	createTestHGT(t, dir, "N48E002.hgt", srtm3Size, func(row, col int) int16 {
		return 500
	})
	// Create N47E002 (south neighbor, lat origin=47) with elevation=700
	createTestHGT(t, dir, "N47E002.hgt", srtm3Size, func(row, col int) int16 {
		return 700
	})

	src := NewSource(dir)

	// Point near the south boundary of N48E002: lat very close to 48.0
	// lat=48.00001 is in N48E002 (floor=48), row ≈ GridSize-1
	ele, ok := src.Elevation(48.00001, 2.5)
	if !ok {
		t.Fatal("expected ok=true for cross-tile south boundary point")
	}
	// Should be interpolated between 500 (primary) and 700 (south neighbor).
	// The point is very close to the boundary, so mostly from primary tile.
	if ele < 500 || ele > 710 {
		t.Errorf("expected elevation between 500 and 710, got %f", ele)
	}
}

func TestSource_CrossTileElevation_SE(t *testing.T) {
	dir := t.TempDir()

	// Create 4 tiles around the corner (49,3):
	// N48E002 (primary), N48E003 (east), N47E002 (south), N47E003 (SE)
	createTestHGT(t, dir, "N48E002.hgt", srtm3Size, func(row, col int) int16 {
		return 500
	})
	createTestHGT(t, dir, "N48E003.hgt", srtm3Size, func(row, col int) int16 {
		return 600
	})
	createTestHGT(t, dir, "N47E002.hgt", srtm3Size, func(row, col int) int16 {
		return 700
	})
	createTestHGT(t, dir, "N47E003.hgt", srtm3Size, func(row, col int) int16 {
		return 800
	})

	src := NewSource(dir)

	// Point near the SE corner of N48E002: lat≈48.00001, lon≈2.99999
	ele, ok := src.Elevation(48.00001, 2.99999)
	if !ok {
		t.Fatal("expected ok=true for cross-tile SE corner point")
	}
	// All 4 tiles contribute. Primary has 500, and the interpolation
	// should be between 500 and 800.
	if ele < 499 || ele > 801 {
		t.Errorf("expected elevation between 499 and 801, got %f", ele)
	}
}

func TestSource_CrossTileElevation_MissingNeighbor(t *testing.T) {
	dir := t.TempDir()

	// Create only N48E002 — east neighbor N48E003 is missing.
	// HGT tiles share their boundary samples (overlap by 1 pixel),
	// so a point near the boundary never actually needs the adjacent tile.
	// This test verifies the elevation succeeds even without the neighbor.
	createTestHGT(t, dir, "N48E002.hgt", srtm3Size, func(row, col int) int16 {
		return 500
	})

	src := NewSource(dir)

	// Point near the east boundary: lon=2.99999 uses cols 1199 and 1200,
	// both within the 1201-sample grid — no cross-tile needed.
	ele, ok := src.Elevation(48.5, 2.99999)
	if !ok {
		t.Fatal("expected ok=true: boundary sample is in the primary tile (HGT overlap)")
	}
	if ele != 500 {
		t.Errorf("expected 500, got %f", ele)
	}
}

func TestTileCachePath_Panic(t *testing.T) {
	defer func() {
		if r := recover(); r == nil {
			t.Error("expected panic for short key")
		}
	}()
	TileCachePath("/cache", "AB") // len < 3, should panic
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
