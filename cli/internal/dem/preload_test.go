package dem

import (
	"net/http"
	"net/http/httptest"
	"os"
	"path/filepath"
	"testing"

	"github.com/jchable/gpx-utility-analyzer/cli/internal/gpx"
)

func TestPreloadTiles_CollectKeys(t *testing.T) {
	points := []gpx.TrackPoint{
		{Lat: 48.5, Lon: 2.5},
		{Lat: 48.9, Lon: 2.9},   // same tile N48E002
		{Lat: 49.5, Lon: 3.5},   // different tile N49E003
		{Lat: -34.5, Lon: 18.5}, // S35E018
	}
	keys := collectTileKeys(points)
	if len(keys) != 3 {
		t.Errorf("expected 3 unique keys, got %d: %v", len(keys), keys)
	}
}

func TestPreloadTiles_LocalTiles(t *testing.T) {
	dir := t.TempDir()
	createTestHGT(t, dir, "N48E002.hgt", srtm3Size, func(row, col int) int16 {
		return 500
	})

	src := NewSource(dir)
	points := []gpx.TrackPoint{
		{Lat: 48.5, Lon: 2.5, Ele: 999},
	}

	if err := src.Preload(points); err != nil {
		t.Fatalf("Preload failed: %v", err)
	}

	// Tile should now be pre-loaded.
	if len(src.tiles) != 1 {
		t.Errorf("expected 1 preloaded tile, got %d", len(src.tiles))
	}

	// Elevation should use the preloaded tile.
	for i := range points {
		if ele, ok := src.Elevation(points[i].Lat, points[i].Lon); ok {
			points[i].Ele = ele
		}
	}
	if points[0].Ele != 500 {
		t.Errorf("expected 500, got %f", points[0].Ele)
	}
}

func TestPreloadTiles_MemoryLimitExceeded(t *testing.T) {
	dir := t.TempDir()
	// Create two SRTM3 tiles (~2.8 MB each, ~5.6 MB total).
	createTestHGT(t, dir, "N48E002.hgt", srtm3Size, func(row, col int) int16 {
		return 500
	})
	createTestHGT(t, dir, "N49E003.hgt", srtm3Size, func(row, col int) int16 {
		return 600
	})

	// Set memory limit to 1 MB — not enough for even one SRTM3 tile (~2.8 MB).
	src := NewSource(dir).WithMaxMemory(1)
	points := []gpx.TrackPoint{
		{Lat: 48.5, Lon: 2.5},
		{Lat: 49.5, Lon: 3.5},
	}

	err := src.Preload(points)
	if err == nil {
		t.Fatal("expected error when memory limit exceeded")
	}
	// Verify error message contains useful info.
	if err.Error() == "" {
		t.Error("expected non-empty error message")
	}
}

func TestPreloadTiles_MemoryLimitOK(t *testing.T) {
	dir := t.TempDir()
	createTestHGT(t, dir, "N48E002.hgt", srtm3Size, func(row, col int) int16 {
		return 500
	})

	// Set memory limit high enough (10 MB > 2.8 MB).
	src := NewSource(dir).WithMaxMemory(10)
	points := []gpx.TrackPoint{
		{Lat: 48.5, Lon: 2.5},
	}

	if err := src.Preload(points); err != nil {
		t.Fatalf("Preload should succeed: %v", err)
	}
}

func TestPreloadTiles_NoLimit(t *testing.T) {
	dir := t.TempDir()
	createTestHGT(t, dir, "N48E002.hgt", srtm3Size, func(row, col int) int16 {
		return 500
	})

	// maxMemoryMB = 0 (default) = no limit.
	src := NewSource(dir)
	points := []gpx.TrackPoint{
		{Lat: 48.5, Lon: 2.5},
	}

	if err := src.Preload(points); err != nil {
		t.Fatalf("Preload should succeed with no limit: %v", err)
	}
}

func TestPreloadTiles_ParallelDownload(t *testing.T) {
	// Create two different gzipped HGT payloads.
	payload1 := createGzippedHGT(t, 800)
	payload2 := createGzippedHGT(t, 900)

	var requestCount int
	srv := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		requestCount++
		w.Header().Set("Content-Type", "application/gzip")
		// Serve different payloads based on URL.
		if filepath.Base(r.URL.Path) == "N48E002.hgt.gz" {
			w.Write(payload1)
		} else {
			w.Write(payload2)
		}
	}))
	defer srv.Close()

	origURL := baseURL
	baseURL = srv.URL
	defer func() { baseURL = origURL }()

	cacheDir := t.TempDir()
	src := NewAutoSource(cacheDir)

	points := []gpx.TrackPoint{
		{Lat: 48.5, Lon: 2.5}, // N48E002
		{Lat: 49.5, Lon: 3.5}, // N49E003
	}

	if err := src.Preload(points); err != nil {
		t.Fatalf("Preload failed: %v", err)
	}

	// Both tiles should be downloaded and loaded.
	if requestCount < 2 {
		t.Errorf("expected at least 2 HTTP requests, got %d", requestCount)
	}

	// Verify hierarchical cache structure.
	hi1 := TileCachePath(cacheDir, "N48E002")
	hi2 := TileCachePath(cacheDir, "N49E003")
	if _, err := os.Stat(hi1); err != nil {
		t.Errorf("expected hierarchical cache file at %s: %v", hi1, err)
	}
	if _, err := os.Stat(hi2); err != nil {
		t.Errorf("expected hierarchical cache file at %s: %v", hi2, err)
	}

	// Verify tiles are in memory.
	if len(src.tiles) != 2 {
		t.Errorf("expected 2 tiles in memory, got %d", len(src.tiles))
	}
}

func TestPreloadTiles_EmptyPoints(t *testing.T) {
	src := NewSource(t.TempDir())
	if err := src.Preload(nil); err != nil {
		t.Fatalf("Preload should succeed with nil points: %v", err)
	}
	if err := src.Preload([]gpx.TrackPoint{}); err != nil {
		t.Fatalf("Preload should succeed with empty points: %v", err)
	}
}

func TestPreloadTiles_DownloadFailureNonFatal(t *testing.T) {
	// Server returns 404 for all requests.
	srv := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		http.NotFound(w, r)
	}))
	defer srv.Close()

	origURL := baseURL
	baseURL = srv.URL
	defer func() { baseURL = origURL }()

	cacheDir := t.TempDir()
	src := NewAutoSource(cacheDir)

	points := []gpx.TrackPoint{
		{Lat: 48.5, Lon: 2.5},
	}

	// Preload should not return error (download failures are non-fatal).
	if err := src.Preload(points); err != nil {
		t.Fatalf("Preload should not fail on download error: %v", err)
	}

	// The tile should not be in memory.
	key := TileKey(48.5, 2.5)
	if src.tiles[key] != nil {
		t.Error("expected nil tile after download failure")
	}
}

func TestCollectTileKeys_Interior(t *testing.T) {
	// Interior points: only the primary key.
	points := []gpx.TrackPoint{
		{Lat: 48.5, Lon: 2.5},
	}
	keys := collectTileKeys(points)
	if len(keys) != 1 {
		t.Errorf("expected 1 key for interior point, got %d: %v", len(keys), keys)
	}
}

func TestCollectTileKeys_NearEastBoundary(t *testing.T) {
	// Point near east boundary of N48E002: lon close to 3.0
	points := []gpx.TrackPoint{
		{Lat: 48.5, Lon: 2.9999},
	}
	keys := collectTileKeys(points)
	// Should include N48E002 (primary) and N48E003 (east neighbor)
	keySet := make(map[string]bool)
	for _, k := range keys {
		keySet[k] = true
	}
	if !keySet["N48E002"] {
		t.Error("expected N48E002 (primary)")
	}
	if !keySet["N48E003"] {
		t.Error("expected N48E003 (east neighbor)")
	}
	if len(keys) != 2 {
		t.Errorf("expected 2 keys, got %d: %v", len(keys), keys)
	}
}

func TestCollectTileKeys_NearSouthBoundary(t *testing.T) {
	// Point near south boundary of N48E002: lat close to 48.0
	points := []gpx.TrackPoint{
		{Lat: 48.0001, Lon: 2.5},
	}
	keys := collectTileKeys(points)
	keySet := make(map[string]bool)
	for _, k := range keys {
		keySet[k] = true
	}
	if !keySet["N48E002"] {
		t.Error("expected N48E002 (primary)")
	}
	if !keySet["N47E002"] {
		t.Error("expected N47E002 (south neighbor)")
	}
	if len(keys) != 2 {
		t.Errorf("expected 2 keys, got %d: %v", len(keys), keys)
	}
}

func TestCollectTileKeys_NearSECorner(t *testing.T) {
	// Point near SE corner of N48E002: lat≈48.0001, lon≈2.9999
	points := []gpx.TrackPoint{
		{Lat: 48.0001, Lon: 2.9999},
	}
	keys := collectTileKeys(points)
	keySet := make(map[string]bool)
	for _, k := range keys {
		keySet[k] = true
	}
	// Should include all 4: primary, south, east, SE
	expected := []string{"N48E002", "N47E002", "N48E003", "N47E003"}
	for _, exp := range expected {
		if !keySet[exp] {
			t.Errorf("expected key %s in set %v", exp, keys)
		}
	}
	if len(keys) != 4 {
		t.Errorf("expected 4 keys, got %d: %v", len(keys), keys)
	}
}

// createGzippedHGT is re-declared here via the test helper in download_test.go
// but since we're in the same package, we can use it directly.
