package dem

import (
	"encoding/binary"
	"fmt"
	"math"
	"os"
	"path/filepath"
	"strings"
)

const (
	srtm1Size = 3601 // 1 arc-second resolution
	srtm3Size = 1201 // 3 arc-second resolution
	voidValue = -32768
)

// Tile represents a loaded SRTM HGT tile.
type Tile struct {
	LatOrigin int     // integer latitude of SW corner
	LonOrigin int     // integer longitude of SW corner
	GridSize  int     // 1201 or 3601
	Data      []int16 // row-major, NW corner first (row 0 = north edge)
}

// TileKey returns the HGT filename stem for a given lat/lon, e.g. "N48W003".
func TileKey(lat, lon float64) string {
	latInt := int(math.Floor(lat))
	lonInt := int(math.Floor(lon))
	ns := "N"
	if latInt < 0 {
		ns = "S"
		latInt = -latInt
	}
	ew := "E"
	if lonInt < 0 {
		ew = "W"
		lonInt = -lonInt
	}
	return fmt.Sprintf("%s%02d%s%03d", ns, latInt, ew, lonInt)
}

// LoadTile reads an HGT file and returns a Tile.
// The grid size is detected from the file size (SRTM1 = 3601x3601, SRTM3 = 1201x1201).
func LoadTile(path string) (*Tile, error) {
	info, err := os.Stat(path)
	if err != nil {
		return nil, fmt.Errorf("stat %s: %w", path, err)
	}

	fileSize := info.Size()
	totalSamples := fileSize / 2
	gridSize := int(math.Sqrt(float64(totalSamples)))

	if gridSize != srtm1Size && gridSize != srtm3Size {
		return nil, fmt.Errorf("invalid HGT file size %d bytes (expected SRTM1 or SRTM3)", fileSize)
	}

	f, err := os.Open(path)
	if err != nil {
		return nil, fmt.Errorf("opening %s: %w", path, err)
	}
	defer f.Close()

	data := make([]int16, gridSize*gridSize)
	if err := binary.Read(f, binary.BigEndian, data); err != nil {
		return nil, fmt.Errorf("reading %s: %w", path, err)
	}

	latOrigin, lonOrigin, err := parseFilename(filepath.Base(path))
	if err != nil {
		return nil, err
	}

	return &Tile{
		LatOrigin: latOrigin,
		LonOrigin: lonOrigin,
		GridSize:  gridSize,
		Data:      data,
	}, nil
}

// Elevation returns the bilinearly interpolated elevation at the given lat/lon.
// Returns (elevation, true) on success, or (0, false) if the point is void or out of bounds.
func (t *Tile) Elevation(lat, lon float64) (float64, bool) {
	// Convert to grid coordinates. Row 0 = north edge.
	row := float64(t.GridSize-1) * (float64(t.LatOrigin+1) - lat)
	col := float64(t.GridSize-1) * (lon - float64(t.LonOrigin))

	if row < 0 || row > float64(t.GridSize-1) || col < 0 || col > float64(t.GridSize-1) {
		return 0, false
	}

	r0 := int(math.Floor(row))
	c0 := int(math.Floor(col))
	r1 := r0 + 1
	c1 := c0 + 1

	// Clamp to grid bounds
	if r1 >= t.GridSize {
		r1 = t.GridSize - 1
	}
	if c1 >= t.GridSize {
		c1 = t.GridSize - 1
	}

	// Read four corners
	q11 := t.get(r0, c0)
	q12 := t.get(r0, c1)
	q21 := t.get(r1, c0)
	q22 := t.get(r1, c1)

	// Check for void values
	if q11 == voidValue || q12 == voidValue || q21 == voidValue || q22 == voidValue {
		return 0, false
	}

	// Fractional offsets
	dr := row - float64(r0)
	dc := col - float64(c0)

	// Bilinear interpolation
	top := float64(q11)*(1-dc) + float64(q12)*dc
	bot := float64(q21)*(1-dc) + float64(q22)*dc
	return top*(1-dr) + bot*dr, true
}

func (t *Tile) get(row, col int) int16 {
	return t.Data[row*t.GridSize+col]
}

// parseFilename extracts lat/lon origin from an HGT filename like "N48W003.hgt".
func parseFilename(name string) (lat, lon int, err error) {
	name = strings.TrimSuffix(strings.ToUpper(name), ".HGT")
	if len(name) != 7 {
		return 0, 0, fmt.Errorf("invalid HGT filename: %s", name)
	}

	ns := name[0]
	latStr := name[1:3]
	ew := name[3]
	lonStr := name[4:7]

	if _, err := fmt.Sscanf(latStr, "%d", &lat); err != nil {
		return 0, 0, fmt.Errorf("parsing latitude from %s: %w", name, err)
	}
	if _, err := fmt.Sscanf(lonStr, "%d", &lon); err != nil {
		return 0, 0, fmt.Errorf("parsing longitude from %s: %w", name, err)
	}

	if ns == 'S' {
		lat = -lat
	}
	if ew == 'W' {
		lon = -lon
	}

	return lat, lon, nil
}
