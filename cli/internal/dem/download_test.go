package dem

import (
	"compress/gzip"
	"encoding/binary"
	"net/http"
	"net/http/httptest"
	"os"
	"path/filepath"
	"testing"
)

// createGzippedHGT creates an in-memory gzipped SRTM3 HGT payload with constant elevation.
func createGzippedHGT(t *testing.T, elevation int16) []byte {
	t.Helper()
	// Create temp file to build gzipped content
	tmp, err := os.CreateTemp(t.TempDir(), "hgt-*.gz")
	if err != nil {
		t.Fatalf("creating temp: %v", err)
	}
	defer tmp.Close()

	gz := gzip.NewWriter(tmp)
	data := make([]int16, srtm3Size*srtm3Size)
	for i := range data {
		data[i] = elevation
	}
	if err := binary.Write(gz, binary.BigEndian, data); err != nil {
		t.Fatalf("writing hgt data: %v", err)
	}
	gz.Close()
	tmp.Close()

	content, err := os.ReadFile(tmp.Name())
	if err != nil {
		t.Fatalf("reading temp: %v", err)
	}
	return content
}

func TestTileURL(t *testing.T) {
	got := tileURL("N48E002")
	expected := "https://elevation-tiles-prod.s3.amazonaws.com/skadi/N48/N48E002.hgt.gz"
	if got != expected {
		t.Errorf("tileURL(N48E002) = %q, expected %q", got, expected)
	}

	got = tileURL("S35W120")
	expected = "https://elevation-tiles-prod.s3.amazonaws.com/skadi/S35/S35W120.hgt.gz"
	if got != expected {
		t.Errorf("tileURL(S35W120) = %q, expected %q", got, expected)
	}
}

func TestDownloadTile_Success(t *testing.T) {
	payload := createGzippedHGT(t, 750)

	srv := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/gzip")
		w.Write(payload)
	}))
	defer srv.Close()

	// Override tileURL by using downloadTileFromURL directly
	cacheDir := t.TempDir()
	destPath := filepath.Join(cacheDir, "N48E002.hgt")

	err := doDownload(&http.Client{}, srv.URL+"/skadi/N48/N48E002.hgt.gz", destPath)
	if err != nil {
		t.Fatalf("doDownload failed: %v", err)
	}

	// Verify file exists and is correct size
	info, err := os.Stat(destPath)
	if err != nil {
		t.Fatalf("stat failed: %v", err)
	}
	if info.Size() != expectedSRTM3Size {
		t.Errorf("expected file size %d, got %d", expectedSRTM3Size, info.Size())
	}

	// Verify tile can be loaded
	tile, err := LoadTile(destPath)
	if err != nil {
		t.Fatalf("LoadTile failed: %v", err)
	}
	ele, ok := tile.Elevation(48.5, 2.5)
	if !ok {
		t.Fatal("expected elevation lookup to succeed")
	}
	if ele != 750 {
		t.Errorf("expected elevation 750, got %f", ele)
	}
}

func TestDownloadTile_HTTP404(t *testing.T) {
	srv := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		http.NotFound(w, r)
	}))
	defer srv.Close()

	destPath := filepath.Join(t.TempDir(), "N99E099.hgt")
	err := doDownload(&http.Client{}, srv.URL+"/skadi/N99/N99E099.hgt.gz", destPath)
	if err == nil {
		t.Fatal("expected error for 404 response")
	}
}

func TestSource_AutoDownloadFallbackGPS(t *testing.T) {
	// Source with auto-download but server returns 404 → should fall back to GPS
	srv := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		http.NotFound(w, r)
	}))
	defer srv.Close()

	// Override base URL to point to test server
	origURL := baseURL
	baseURL = srv.URL
	defer func() { baseURL = origURL }()

	cacheDir := t.TempDir()
	src := NewAutoSource(cacheDir)

	// This should not panic, just return false
	_, ok := src.Lookup(48.5, 2.5)
	if ok {
		t.Error("expected ok=false when download fails")
	}
}

func TestSource_AutoDownloadSuccess(t *testing.T) {
	payload := createGzippedHGT(t, 800)

	srv := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/gzip")
		w.Write(payload)
	}))
	defer srv.Close()

	origURL := baseURL
	baseURL = srv.URL
	defer func() { baseURL = origURL }()

	cacheDir := t.TempDir()
	src := NewAutoSource(cacheDir)

	ele, ok := src.Lookup(48.5, 2.5)
	if !ok {
		t.Fatal("expected ok=true after auto-download")
	}
	if ele != 800 {
		t.Errorf("expected elevation 800, got %f", ele)
	}

	// Verify tile is cached on disk (hierarchical path)
	cachePath := TileCachePath(cacheDir, "N48E002")
	if _, err := os.Stat(cachePath); err != nil {
		t.Errorf("expected cached tile at %s: %v", cachePath, err)
	}

	// Second lookup should use cache (no HTTP call)
	srv.Close()
	ele2, ok2 := src.Lookup(48.5, 2.5)
	if !ok2 {
		t.Fatal("expected ok=true from cached tile")
	}
	if ele2 != 800 {
		t.Errorf("expected cached elevation 800, got %f", ele2)
	}
}

func TestDefaultCacheDir(t *testing.T) {
	dir := DefaultCacheDir()
	if dir == "" {
		t.Error("DefaultCacheDir returned empty string")
	}
	// Just verify it ends with the expected path
	if filepath.Base(dir) != "srtm" {
		t.Errorf("expected cache dir to end with 'srtm', got %q", dir)
	}
}
