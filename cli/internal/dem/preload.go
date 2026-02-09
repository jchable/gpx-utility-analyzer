package dem

import (
	"fmt"
	"log/slog"
	"math"
	"os"
	"path/filepath"
	"sync"

	"github.com/jchable/gpx-utility-analyzer/cli/internal/gpx"
)

const defaultParallelDownloads = 4

// Preload identifies all DEM tiles needed for the given track points,
// ensures they are available on disk (downloading in parallel if needed),
// checks the memory limit, then loads them all into memory.
//
// This must be called before using Elevation for optimal performance.
// Returns an error if the required tiles exceed the configured memory limit.
func (s *Source) Preload(points []gpx.TrackPoint) error {
	// 1. Collect unique tile keys needed.
	needed := collectTileKeys(points)
	if len(needed) == 0 {
		return nil
	}

	// 2. Ensure all tiles are on disk (download missing ones in parallel).
	if err := ensureTilesOnDisk(needed, s); err != nil {
		return err
	}

	// 3. Determine tile file sizes and check memory limit.
	tileFiles, totalBytes := resolveTileFiles(needed, s)

	if s.maxMemoryMB > 0 {
		limitBytes := int64(s.maxMemoryMB) * 1024 * 1024
		if totalBytes > limitBytes {
			totalMB := totalBytes / (1024 * 1024)
			return fmt.Errorf(
				"DEM tiles require ~%d MB in memory (%d tiles), but --dem-max-memory is set to %d MB; "+
					"increase the limit or disable DEM correction with --dem-auto-download=false",
				totalMB, len(tileFiles), s.maxMemoryMB,
			)
		}
	}

	// 4. Load all tiles into memory.
	for key, path := range tileFiles {
		if _, ok := s.tiles[key]; ok {
			continue // already loaded
		}
		if tile := s.loadAndValidate(path, key); tile != nil {
			s.tiles[key] = tile
		} else {
			s.tiles[key] = nil
		}
	}

	return nil
}

// boundaryThreshold is the fractional degree threshold for detecting tile
// boundary proximity. Based on SRTM3 (1201 grid): 1/1200 ≈ 0.000833°.
// For SRTM1 (3601 grid), the actual boundary zone is ~3× smaller, so this
// conservatively preloads a few extra tiles for SRTM1 tracks — acceptable.
const boundaryThreshold = 1.0 / 1200.0

// collectTileKeys returns a deduplicated list of tile keys needed for all points,
// including neighbor tiles for points near tile boundaries (cross-tile interpolation).
func collectTileKeys(points []gpx.TrackPoint) []string {
	seen := make(map[string]bool)
	add := func(key string) {
		if !seen[key] {
			seen[key] = true
		}
	}

	for _, p := range points {
		key := TileKey(p.Lat, p.Lon)
		add(key)

		// Check proximity to tile boundaries for cross-tile interpolation.
		latFloor := math.Floor(p.Lat)
		lonFloor := math.Floor(p.Lon)

		nearSouth := p.Lat-latFloor < boundaryThreshold && p.Lat > latFloor
		nearEast := (lonFloor+1)-p.Lon < boundaryThreshold && p.Lon < lonFloor+1

		if nearSouth {
			add(TileKey(latFloor-0.5, p.Lon)) // south neighbor
		}
		if nearEast {
			add(TileKey(p.Lat, lonFloor+1.5)) // east neighbor
		}
		if nearSouth && nearEast {
			add(TileKey(latFloor-0.5, lonFloor+1.5)) // SE neighbor
		}
	}

	keys := make([]string, 0, len(seen))
	for key := range seen {
		keys = append(keys, key)
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

	src.log.Info("downloading DEM tiles in parallel", slog.Int("count", len(toDownload)))

	// Parallel download with bounded concurrency.
	type downloadResult struct {
		key string
		err error
	}
	sem := make(chan struct{}, defaultParallelDownloads)
	var mu sync.Mutex
	var results []downloadResult
	var wg sync.WaitGroup

	for _, key := range toDownload {
		wg.Add(1)
		go func(k string) {
			defer wg.Done()
			sem <- struct{}{}
			defer func() { <-sem }()

			destPath := TileCachePath(src.cacheDir, k)
			if err := downloadTile(k, destPath); err != nil {
				mu.Lock()
				results = append(results, downloadResult{k, fmt.Errorf("tile %s: %w", k, err)})
				mu.Unlock()
				return
			}
			mu.Lock()
			results = append(results, downloadResult{k, nil})
			mu.Unlock()
		}(key)
	}
	wg.Wait()

	// Log results sequentially (no interleaving).
	for _, r := range results {
		if r.err != nil {
			src.log.Warn("download failed, using GPS elevation", slog.String("tile", r.key), slog.Any("error", r.err))
		} else {
			src.log.Info("downloaded DEM tile", slog.String("tile", r.key))
		}
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
func resolveTileFiles(keys []string, src *Source) (map[string]string, int64) {
	tileFiles := make(map[string]string, len(keys))
	var totalBytes int64

	for _, key := range keys {
		path := findTilePath(key, src)
		if path == "" {
			// Tile not on disk (download failed or not available).
			if !src.warns[key] {
				src.log.Warn("DEM tile not available, using GPS elevation", slog.String("tile", key))
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

	return tileFiles, totalBytes
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
