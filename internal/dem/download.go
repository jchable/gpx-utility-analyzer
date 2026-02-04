package dem

import (
	"compress/gzip"
	"fmt"
	"io"
	"net/http"
	"os"
	"path/filepath"
	"time"
)

const (
	// SRTM3 HGT file size: 1201 * 1201 * 2 bytes = 2,884,802 bytes
	expectedSRTM3Size int64 = 2 * srtm3Size * srtm3Size
	// SRTM1 HGT file size: 3601 * 3601 * 2 bytes = 25,934,402 bytes
	expectedSRTM1Size int64 = 2 * srtm1Size * srtm1Size

	downloadTimeout = 60 * time.Second
	maxRetries      = 2
)

// baseURL is the base URL for downloading SRTM tiles. Override in tests.
var baseURL = "https://elevation-tiles-prod.s3.amazonaws.com/skadi"

// tileURL returns the download URL for a given tile key from the Mapzen/AWS elevation tiles.
// Format: https://elevation-tiles-prod.s3.amazonaws.com/skadi/N48/N48E002.hgt.gz
func tileURL(key string) string {
	// key is e.g. "N48E002", prefix is "N48"
	prefix := key[:3]
	return fmt.Sprintf("%s/%s/%s.hgt.gz", baseURL, prefix, key)
}

// downloadTile downloads a .hgt.gz file from the elevation tiles service,
// decompresses it, validates the file size, and writes it to destPath.
// Returns nil on success. The parent directory is created if needed.
func downloadTile(key, destPath string) error {
	if err := os.MkdirAll(filepath.Dir(destPath), 0o755); err != nil {
		return fmt.Errorf("creating cache dir: %w", err)
	}

	url := tileURL(key)
	var lastErr error

	client := &http.Client{Timeout: downloadTimeout}

	for attempt := 0; attempt <= maxRetries; attempt++ {
		if attempt > 0 {
			time.Sleep(time.Duration(attempt) * time.Second)
		}

		lastErr = doDownload(client, url, destPath)
		if lastErr == nil {
			return nil
		}
	}

	// Clean up partial file on failure
	os.Remove(destPath)
	return fmt.Errorf("downloading %s after %d attempts: %w", key, maxRetries+1, lastErr)
}

func doDownload(client *http.Client, url, destPath string) error {
	resp, err := client.Get(url)
	if err != nil {
		return fmt.Errorf("GET %s: %w", url, err)
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		return fmt.Errorf("GET %s: HTTP %d", url, resp.StatusCode)
	}

	gz, err := gzip.NewReader(resp.Body)
	if err != nil {
		return fmt.Errorf("gzip reader: %w", err)
	}
	defer gz.Close()

	tmpPath := destPath + ".tmp"
	f, err := os.Create(tmpPath)
	if err != nil {
		return fmt.Errorf("creating temp file: %w", err)
	}

	n, err := io.Copy(f, gz)
	f.Close()
	if err != nil {
		os.Remove(tmpPath)
		return fmt.Errorf("decompressing: %w", err)
	}

	if n != expectedSRTM1Size && n != expectedSRTM3Size {
		os.Remove(tmpPath)
		return fmt.Errorf("unexpected file size %d bytes (expected SRTM1 or SRTM3)", n)
	}

	if err := os.Rename(tmpPath, destPath); err != nil {
		os.Remove(tmpPath)
		return fmt.Errorf("renaming temp file: %w", err)
	}

	return nil
}
