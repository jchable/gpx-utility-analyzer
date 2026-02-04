package gpx

import (
	"encoding/xml"
	"fmt"
	"io"
	"os"
)

// ParseFile reads and parses a GPX file from the given path.
func ParseFile(path string) (*GPX, error) {
	f, err := os.Open(path)
	if err != nil {
		return nil, fmt.Errorf("opening %s: %w", path, err)
	}
	defer f.Close()
	return Parse(f)
}

// Parse reads and parses GPX data from the given reader.
func Parse(r io.Reader) (*GPX, error) {
	var g GPX
	decoder := xml.NewDecoder(r)
	if err := decoder.Decode(&g); err != nil {
		return nil, fmt.Errorf("decoding GPX: %w", err)
	}
	if g.PointCount() == 0 {
		return nil, fmt.Errorf("no trackpoints found in GPX data")
	}
	return &g, nil
}
