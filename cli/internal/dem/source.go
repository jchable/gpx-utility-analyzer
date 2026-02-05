package dem

import (
	"fmt"
	"os"
	"path/filepath"
	"runtime"
)

// Source provides DEM elevation lookups from a directory of HGT files.
// It supports optional auto-downloading of missing tiles.
type Source struct {
	dir          string
	cacheDir     string // secondary search path + download destination
	autoDownload bool
	tiles        map[string]*Tile
	warns        map[string]bool
}

// NewSource creates a new DEM source backed by the given directory of .hgt files.
// Auto-download is disabled.
func NewSource(dir string) *Source {
	return &Source{
		dir:   dir,
		tiles: make(map[string]*Tile),
		warns: make(map[string]bool),
	}
}

// NewSourceWithCache creates a DEM source that first looks in dir, then in cacheDir.
// If autoDownload is true, missing tiles are fetched from the elevation tiles service
// and stored in cacheDir.
func NewSourceWithCache(dir, cacheDir string, autoDownload bool) *Source {
	return &Source{
		dir:          dir,
		cacheDir:     cacheDir,
		autoDownload: autoDownload,
		tiles:        make(map[string]*Tile),
		warns:        make(map[string]bool),
	}
}

// NewAutoSource creates a DEM source that auto-downloads tiles into cacheDir.
// No local user directory is searched first.
func NewAutoSource(cacheDir string) *Source {
	return &Source{
		cacheDir:     cacheDir,
		autoDownload: true,
		tiles:        make(map[string]*Tile),
		warns:        make(map[string]bool),
	}
}

// DefaultCacheDir returns the default cache directory for DEM tiles.
// On Windows: %LOCALAPPDATA%\gpx-utility-analyzer\srtm
// On others: ~/.cache/gpx-utility-analyzer/srtm
func DefaultCacheDir() string {
	if runtime.GOOS == "windows" {
		if dir := os.Getenv("LOCALAPPDATA"); dir != "" {
			return filepath.Join(dir, "gpx-utility-analyzer", "srtm")
		}
	}
	if dir, err := os.UserCacheDir(); err == nil {
		return filepath.Join(dir, "gpx-utility-analyzer", "srtm")
	}
	home, _ := os.UserHomeDir()
	return filepath.Join(home, ".cache", "gpx-utility-analyzer", "srtm")
}

// Lookup returns the DEM elevation for a lat/lon, loading tiles on demand.
// Returns (elevation, true) on success, or (0, false) if tile is missing or void.
func (s *Source) Lookup(lat, lon float64) (float64, bool) {
	key := TileKey(lat, lon)
	tile, ok := s.tiles[key]
	if !ok {
		tile = s.loadTile(key)
		s.tiles[key] = tile
	}
	if tile == nil {
		return 0, false
	}
	return tile.Elevation(lat, lon)
}

// loadTile tries to load a tile from disk, and optionally downloads it.
func (s *Source) loadTile(key string) *Tile {
	// Try user-provided directory first
	if s.dir != "" {
		path := filepath.Join(s.dir, key+".hgt")
		if tile, err := LoadTile(path); err == nil {
			return tile
		}
	}

	// Try cache directory
	if s.cacheDir != "" {
		cachePath := filepath.Join(s.cacheDir, key+".hgt")
		if tile, err := LoadTile(cachePath); err == nil {
			return tile
		}

		// Auto-download if enabled
		if s.autoDownload {
			fmt.Fprintf(os.Stderr, "Downloading DEM tile %s...\n", key)
			if err := downloadTile(key, cachePath); err != nil {
				if !s.warns[key] {
					fmt.Fprintf(os.Stderr, "Warning: could not download tile %s: %v, using GPS elevation\n", key, err)
					s.warns[key] = true
				}
				return nil
			}
			fmt.Fprintf(os.Stderr, "Downloaded DEM tile %s\n", key)
			if tile, err := LoadTile(cachePath); err == nil {
				return tile
			}
		}
	}

	if !s.warns[key] {
		fmt.Fprintf(os.Stderr, "Warning: DEM tile %s not available, using GPS elevation\n", key)
		s.warns[key] = true
	}
	return nil
}
