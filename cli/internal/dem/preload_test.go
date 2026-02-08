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

	if err := PreloadTiles(points, src); err != nil {
		t.Fatalf("PreloadTiles failed: %v", err)
	}

	// Tile should now be pre-loaded.
	if len(src.tiles) != 1 {
		t.Errorf("expected 1 preloaded tile, got %d", len(src.tiles))
	}

	// CorrectElevations should use the preloaded tile.
	CorrectElevations(points, src)
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

	err := PreloadTiles(points, src)
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

	if err := PreloadTiles(points, src); err != nil {
		t.Fatalf("PreloadTiles should succeed: %v", err)
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

	if err := PreloadTiles(points, src); err != nil {
		t.Fatalf("PreloadTiles should succeed with no limit: %v", err)
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

	if err := PreloadTiles(points, src); err != nil {
		t.Fatalf("PreloadTiles failed: %v", err)
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
	if err := PreloadTiles(nil, src); err != nil {
		t.Fatalf("PreloadTiles should succeed with nil points: %v", err)
	}
	if err := PreloadTiles([]gpx.TrackPoint{}, src); err != nil {
		t.Fatalf("PreloadTiles should succeed with empty points: %v", err)
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

	// PreloadTiles should not return error (download failures are non-fatal).
	if err := PreloadTiles(points, src); err != nil {
		t.Fatalf("PreloadTiles should not fail on download error: %v", err)
	}

	// The tile should not be in memory.
	key := TileKey(48.5, 2.5)
	if src.tiles[key] != nil {
		t.Error("expected nil tile after download failure")
	}
}

// createGzippedHGT is re-declared here via the test helper in download_test.go
// but since we're in the same package, we can use it directly.
