package dem

import (
	"fmt"
	"os"
	"path/filepath"
	"sync"

	"github.com/jchable/gpx-utility-analyzer/cli/internal/gpx"
)

const defaultParallelDownloads = 4

// PreloadTiles identifies all DEM tiles needed for the given track points,
// ensures they are available on disk (downloading in parallel if needed),
// checks the memory limit, then loads them all into memory.
//
// This must be called before CorrectElevations for optimal performance.
// Returns an error if the required tiles exceed the configured memory limit.
func PreloadTiles(points []gpx.TrackPoint, src *Source) error {
	// 1. Collect unique tile keys needed.
	needed := collectTileKeys(points)
	if len(needed) == 0 {
		return nil
	}

	// 2. Ensure all tiles are on disk (download missing ones in parallel).
	if err := ensureTilesOnDisk(needed, src); err != nil {
		return err
	}

	// 3. Determine tile file sizes and check memory limit.
	tileFiles, totalBytes, err := resolveTileFiles(needed, src)
	if err != nil {
		return err
	}

	if src.maxMemoryMB > 0 {
		limitBytes := int64(src.maxMemoryMB) * 1024 * 1024
		if totalBytes > limitBytes {
			totalMB := totalBytes / (1024 * 1024)
			return fmt.Errorf(
				"DEM tiles require ~%d MB in memory (%d tiles), but --dem-max-memory is set to %d MB; "+
					"increase the limit or disable DEM correction with --dem-auto-download=false",
				totalMB, len(tileFiles), src.maxMemoryMB,
			)
		}
	}

	// 4. Load all tiles into memory.
	for key, path := range tileFiles {
		if _, ok := src.tiles[key]; ok {
			continue // already loaded
		}
		tile, err := LoadTile(path)
		if err != nil {
			fmt.Fprintf(os.Stderr, "Warning: could not load tile %s: %v, using GPS elevation\n", key, err)
			src.tiles[key] = nil
			continue
		}
		if !src.skipValidation && !ValidateTile(tile) {
			fmt.Fprintf(os.Stderr, "Warning: tile %s failed validation (all void), ignoring\n", key)
			src.tiles[key] = nil
			continue
		}
		src.tiles[key] = tile
	}

	return nil
}

// collectTileKeys returns a deduplicated list of tile keys needed for all points.
func collectTileKeys(points []gpx.TrackPoint) []string {
	seen := make(map[string]bool)
	var keys []string
	for _, p := range points {
		key := TileKey(p.Lat, p.Lon)
		if !seen[key] {
			seen[key] = true
			keys = append(keys, key)
		}
	}
	return keys
}

// ensureTilesOnDisk downloads missing tiles in parallel using a bounded goroutine pool.
func ensureTilesOnDisk(keys []string, src *Source) error {
	if !src.autoDownload || src.cacheDir == "" {
		return nil // nothing to download
	}

	// Identify which tiles need downloading.
	var toDownload []string
	for _, key := range keys {
		if tileExistsOnDisk(key, src) {
			continue
		}
		toDownload = append(toDownload, key)
	}

	if len(toDownload) == 0 {
		return nil
	}

	fmt.Fprintf(os.Stderr, "Downloading %d DEM tile(s) in parallel...\n", len(toDownload))

	// Parallel download with bounded concurrency.
	sem := make(chan struct{}, defaultParallelDownloads)
	var mu sync.Mutex
	var errs []error
	var wg sync.WaitGroup

	for _, key := range toDownload {
		wg.Add(1)
		go func(k string) {
			defer wg.Done()
			sem <- struct{}{}
			defer func() { <-sem }()

			destPath := TileCachePath(src.cacheDir, k)
			fmt.Fprintf(os.Stderr, "  Downloading tile %s...\n", k)
			if err := downloadTile(k, destPath); err != nil {
				mu.Lock()
				errs = append(errs, fmt.Errorf("tile %s: %w", k, err))
				mu.Unlock()
				return
			}
			fmt.Fprintf(os.Stderr, "  Downloaded tile %s\n", k)
		}(key)
	}
	wg.Wait()

	// Download errors are non-fatal (fallback to GPS elevation).
	for _, err := range errs {
		fmt.Fprintf(os.Stderr, "Warning: %v, using GPS elevation\n", err)
	}

	return nil
}

// tileExistsOnDisk checks if a tile file exists in the user dir or cache dir.
func tileExistsOnDisk(key string, src *Source) bool {
	// Check user-provided directory
	if src.dir != "" {
		path := filepath.Join(src.dir, key+".hgt")
		if _, err := os.Stat(path); err == nil {
			return true
		}
	}
	// Check hierarchical cache path
	if src.cacheDir != "" {
		hiPath := TileCachePath(src.cacheDir, key)
		if _, err := os.Stat(hiPath); err == nil {
			return true
		}
		// Backward compat: check flat path
		flatPath := filepath.Join(src.cacheDir, key+".hgt")
		if _, err := os.Stat(flatPath); err == nil {
			return true
		}
	}
	return false
}

// resolveTileFiles returns a map of key→filepath for all tiles found on disk,
// plus the total memory needed to load them all.
func resolveTileFiles(keys []string, src *Source) (map[string]string, int64, error) {
	tileFiles := make(map[string]string, len(keys))
	var totalBytes int64

	for _, key := range keys {
		path := findTilePath(key, src)
		if path == "" {
			// Tile not on disk (download failed or not available).
			if !src.warns[key] {
				fmt.Fprintf(os.Stderr, "Warning: DEM tile %s not available, using GPS elevation\n", key)
				src.warns[key] = true
			}
			continue
		}

		info, err := os.Stat(path)
		if err != nil {
			continue
		}
		tileFiles[key] = path
		totalBytes += info.Size() // file size == in-memory size for HGT (raw int16 array)
	}

	return tileFiles, totalBytes, nil
}

// findTilePath locates a tile file on disk, checking all possible locations.
func findTilePath(key string, src *Source) string {
	// User-provided directory
	if src.dir != "" {
		path := filepath.Join(src.dir, key+".hgt")
		if _, err := os.Stat(path); err == nil {
			return path
		}
	}
	// Hierarchical cache path
	if src.cacheDir != "" {
		hiPath := TileCachePath(src.cacheDir, key)
		if _, err := os.Stat(hiPath); err == nil {
			return hiPath
		}
		// Flat cache path (backward compat)
		flatPath := filepath.Join(src.cacheDir, key+".hgt")
		if _, err := os.Stat(flatPath); err == nil {
			return flatPath
		}
	}
	return ""
}
