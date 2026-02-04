package dem

import (
	"fmt"
	"os"
	"path/filepath"
)

// Source provides DEM elevation lookups from a directory of HGT files.
type Source struct {
	dir   string
	tiles map[string]*Tile
	warns map[string]bool
}

// NewSource creates a new DEM source backed by the given directory of .hgt files.
func NewSource(dir string) *Source {
	return &Source{
		dir:   dir,
		tiles: make(map[string]*Tile),
		warns: make(map[string]bool),
	}
}

// Lookup returns the DEM elevation for a lat/lon, loading tiles on demand.
// Returns (elevation, true) on success, or (0, false) if tile is missing or void.
func (s *Source) Lookup(lat, lon float64) (float64, bool) {
	key := TileKey(lat, lon)
	tile, ok := s.tiles[key]
	if !ok {
		path := filepath.Join(s.dir, key+".hgt")
		var err error
		tile, err = LoadTile(path)
		if err != nil {
			if !s.warns[key] {
				fmt.Fprintf(os.Stderr, "Warning: DEM tile %s not available, using GPS elevation\n", key)
				s.warns[key] = true
			}
			s.tiles[key] = nil // cache the miss
			return 0, false
		}
		s.tiles[key] = tile
	}
	if tile == nil {
		return 0, false
	}
	return tile.Elevation(lat, lon)
}
