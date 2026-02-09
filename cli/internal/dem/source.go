package dem

import (
	"log/slog"
	"math"
	"os"
	"path/filepath"
	"runtime"
)

// Source provides DEM elevation lookups from a directory of HGT files.
// It supports optional auto-downloading of missing tiles, memory limits,
// and post-download validation.
type Source struct {
	dir            string
	cacheDir       string // secondary search path + download destination
	autoDownload   bool
	maxMemoryMB    int  // 0 = no limit
	skipValidation bool // skip post-download tile validation
	tiles          map[string]*Tile
	warns          map[string]bool
	log            *slog.Logger
}

// NewSource creates a new DEM source backed by the given directory of .hgt files.
// Auto-download is disabled.
func NewSource(dir string) *Source {
	return &Source{
		dir:   dir,
		tiles: make(map[string]*Tile),
		warns: make(map[string]bool),
		log:   slog.Default(),
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
		log:          slog.Default(),
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
		log:          slog.Default(),
	}
}

// WithMaxMemory sets the maximum memory (in MB) allowed for loaded DEM tiles.
// 0 means no limit (default). If the required tiles exceed this limit,
// PreloadTiles will return an error before loading any tile into memory.
func (s *Source) WithMaxMemory(mb int) *Source {
	s.maxMemoryMB = mb
	return s
}

// WithSkipValidation disables post-download tile validation.
// By default, tiles are validated after download to detect corrupt data.
func (s *Source) WithSkipValidation(skip bool) *Source {
	s.skipValidation = skip
	return s
}

// WithLogger sets a custom structured logger for the DEM source.
// If nil, slog.Default() is used.
func (s *Source) WithLogger(l *slog.Logger) *Source {
	if l != nil {
		s.log = l
	}
	return s
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

// TileCachePath returns the hierarchical cache path for a tile key.
// e.g. TileCachePath("/cache", "N48E002") → "/cache/N48/N48E002.hgt"
// Panics if key is shorter than 3 characters (programming error).
func TileCachePath(cacheDir, key string) string {
	if len(key) < 3 {
		panic("dem: invalid tile key: " + key)
	}
	prefix := key[:3] // e.g. "N48"
	return filepath.Join(cacheDir, prefix, key+".hgt")
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

// Elevation returns the DEM elevation for a lat/lon with cross-tile interpolation.
// When a point falls near a tile boundary, data from the adjacent tile is used
// for accurate bilinear interpolation instead of clamping.
func (s *Source) Elevation(lat, lon float64) (float64, bool) {
	key := TileKey(lat, lon)
	tile, ok := s.tiles[key]
	if !ok {
		tile = s.loadTile(key)
		s.tiles[key] = tile
	}
	if tile == nil {
		return 0, false
	}

	// Check if the point is on a tile boundary and needs cross-tile interpolation.
	row := float64(tile.GridSize-1) * (float64(tile.LatOrigin+1) - lat)
	col := float64(tile.GridSize-1) * (lon - float64(tile.LonOrigin))

	if row < 0 || row > float64(tile.GridSize-1) || col < 0 || col > float64(tile.GridSize-1) {
		return 0, false
	}

	r0 := int(math.Floor(row))
	c0 := int(math.Floor(col))
	needSouth := r0+1 >= tile.GridSize && row > float64(r0)
	needEast := c0+1 >= tile.GridSize && col > float64(c0)

	if !needSouth && !needEast {
		// Normal case: all four corners are within this tile.
		return tile.Elevation(lat, lon)
	}

	// Cross-tile interpolation needed.
	return s.crossTileElevation(tile, lat, lon, row, col, r0, c0, needSouth, needEast)
}

// crossTileElevation performs bilinear interpolation across tile boundaries.
func (s *Source) crossTileElevation(tile *Tile, lat, lon, row, col float64, r0, c0 int, needSouth, needEast bool) (float64, bool) {
	gs := tile.GridSize

	// Helper to get a sample, possibly from an adjacent tile.
	getSample := func(r, c int) (int16, bool) {
		if r < gs && c < gs {
			v := tile.get(r, c)
			return v, v != voidValue
		}
		// Determine which adjacent tile to use.
		adjLat := float64(tile.LatOrigin)
		adjLon := float64(tile.LonOrigin)
		if r >= gs {
			adjLat -= 1 // south neighbor
		}
		if c >= gs {
			adjLon += 1 // east neighbor
		}
		adjKey := TileKey(adjLat+0.5, adjLon+0.5)
		adjTile, ok := s.tiles[adjKey]
		if !ok {
			adjTile = s.loadTile(adjKey)
			s.tiles[adjKey] = adjTile
		}
		if adjTile == nil {
			return 0, false
		}
		// Map to adjacent tile coordinates.
		nr, nc := r, c
		if r >= gs {
			nr = 0 // first row of south tile = shared boundary
		}
		if c >= gs {
			nc = 0 // first column of east tile = shared boundary
		}
		v := adjTile.get(nr, nc)
		return v, v != voidValue
	}

	r1 := r0 + 1
	c1 := c0 + 1

	q11, ok1 := getSample(r0, c0)
	q12, ok2 := getSample(r0, c1)
	q21, ok3 := getSample(r1, c0)
	q22, ok4 := getSample(r1, c1)

	if !ok1 || !ok2 || !ok3 || !ok4 {
		return 0, false
	}

	dr := row - float64(r0)
	dc := col - float64(c0)

	top := float64(q11)*(1-dc) + float64(q12)*dc
	bot := float64(q21)*(1-dc) + float64(q22)*dc
	return top*(1-dr) + bot*dr, true
}

// loadAndValidate loads a tile from the given path and validates it.
// Returns the tile if valid, or nil if the file doesn't exist, can't be loaded,
// or fails validation.
func (s *Source) loadAndValidate(path, key string) *Tile {
	tile, err := LoadTile(path)
	if err != nil {
		return nil
	}
	if s.skipValidation || ValidateTile(tile) {
		return tile
	}
	s.log.Warn("tile failed validation (all void), ignoring", slog.String("tile", key), slog.String("path", path))
	return nil
}

// loadTile tries to load a tile from disk, and optionally downloads it.
func (s *Source) loadTile(key string) *Tile {
	// Try user-provided directory first
	if s.dir != "" {
		if tile := s.loadAndValidate(filepath.Join(s.dir, key+".hgt"), key); tile != nil {
			return tile
		}
	}

	// Try cache directory (hierarchical path first, then flat for backward compat)
	if s.cacheDir != "" {
		hiPath := TileCachePath(s.cacheDir, key)
		if tile := s.loadAndValidate(hiPath, key); tile != nil {
			return tile
		}
		// Backward compat: try flat path
		flatPath := filepath.Join(s.cacheDir, key+".hgt")
		if flatPath != hiPath {
			if tile := s.loadAndValidate(flatPath, key); tile != nil {
				return tile
			}
		}

		// Auto-download if enabled (always to hierarchical path)
		if s.autoDownload {
			s.log.Info("downloading DEM tile", slog.String("tile", key))
			if err := downloadTile(key, hiPath); err != nil {
				if !s.warns[key] {
					s.log.Warn("could not download tile, using GPS elevation", slog.String("tile", key), slog.Any("error", err))
					s.warns[key] = true
				}
				return nil
			}
			s.log.Info("downloaded DEM tile", slog.String("tile", key))
			if tile := s.loadAndValidate(hiPath, key); tile != nil {
				return tile
			}
		}
	}

	if !s.warns[key] {
		s.log.Warn("DEM tile not available, using GPS elevation", slog.String("tile", key))
		s.warns[key] = true
	}
	return nil
}

// ValidateTile checks that a tile contains at least one non-void sample.
// It scans a sample of evenly distributed points for efficiency.
func ValidateTile(tile *Tile) bool {
	if tile == nil {
		return false
	}
	// Sample up to 100 points evenly distributed across the grid.
	total := tile.GridSize * tile.GridSize
	step := total / 100
	if step < 1 {
		step = 1
	}
	for i := 0; i < total; i += step {
		if tile.Data[i] != voidValue {
			return true
		}
	}
	return false
}

// TileMemoryBytes returns the in-memory size of a tile with the given grid size.
func TileMemoryBytes(gridSize int) int64 {
	// Each sample is an int16 (2 bytes) + struct overhead (~32 bytes, negligible).
	return int64(gridSize) * int64(gridSize) * 2
}
