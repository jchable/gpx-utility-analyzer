package gpx

import (
	"encoding/xml"
	"fmt"
	"io"
	"os"
	"path/filepath"
)

// WriteFile writes a GPX document to the given path, creating parent directories if needed.
func WriteFile(g *GPX, path string) error {
	dir := filepath.Dir(path)
	if err := os.MkdirAll(dir, 0o755); err != nil {
		return fmt.Errorf("creating directory %s: %w", dir, err)
	}

	f, err := os.Create(path)
	if err != nil {
		return fmt.Errorf("creating %s: %w", path, err)
	}
	defer f.Close()

	return Write(g, f)
}

// Write writes a GPX document to the given writer.
func Write(g *GPX, w io.Writer) error {
	if _, err := fmt.Fprintln(w, `<?xml version="1.0" encoding="UTF-8"?>`); err != nil {
		return err
	}

	enc := xml.NewEncoder(w)
	enc.Indent("", "  ")
	if err := enc.Encode(g); err != nil {
		return fmt.Errorf("encoding GPX: %w", err)
	}
	return nil
}

// NewGPXFromPoints creates a GPX document from a slice of TrackPoints.
func NewGPXFromPoints(points []TrackPoint, name string) *GPX {
	seg := Segment{}
	for _, tp := range points {
		p := Point{
			Lat:   tp.Lat,
			Lon:   tp.Lon,
			Ele:   tp.Ele,
			Speed: tp.Speed,
		}
		if !tp.Time.IsZero() {
			p.RawTime = tp.Time.UTC().Format("2006-01-02T15:04:05Z")
		}
		seg.Points = append(seg.Points, p)
	}

	return &GPX{
		Version: "1.0",
		Creator: "gpx-analyzer",
		Tracks: []Track{
			{
				Name:     name,
				Segments: []Segment{seg},
			},
		},
	}
}
