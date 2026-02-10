package gpx

import (
	"encoding/xml"
	"fmt"
	"io"
	"os"
	"path/filepath"
	"strings"
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
	return newGPX(points, name, false)
}

// NewEnrichedGPXFromPoints creates a GPX document with computed metrics and biometrics as extensions.
// Extensions include speed (m/s), cumulative distance (m), grade (fraction), and any biometrics.
func NewEnrichedGPXFromPoints(points []TrackPoint, name string) *GPX {
	return newGPX(points, name, true)
}

func newGPX(points []TrackPoint, name string, enrich bool) *GPX {
	seg := Segment{}
	var cumDist float64

	for i, tp := range points {
		p := Point{
			Lat:   tp.Lat,
			Lon:   tp.Lon,
			Ele:   tp.Ele,
			Speed: tp.Speed,
		}
		if !tp.Time.IsZero() {
			p.RawTime = tp.Time.UTC().Format("2006-01-02T15:04:05Z")
		}

		if enrich {
			cumDist += tp.DistFromPrev

			// Grade: elevation delta / horizontal distance
			var grade float64
			if i > 0 && tp.DistFromPrev > 1 {
				grade = (tp.Ele - points[i-1].Ele) / tp.DistFromPrev
			}

			p.Extensions.InnerXML = buildEnrichedExtensionsXML(tp.CalcSpeed, cumDist, grade, tp)
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

// buildEnrichedExtensionsXML builds the inner XML for <extensions> containing
// computed metrics and biometric data.
func buildEnrichedExtensionsXML(speed, cumDist, grade float64, tp TrackPoint) string {
	var b strings.Builder

	// Computed metrics
	b.WriteString(`<gpxa:TrackPointMetrics xmlns:gpxa="http://gpx-analyzer.io/extensions/v1">`)
	fmt.Fprintf(&b, `<gpxa:speed>%.4f</gpxa:speed>`, speed)
	fmt.Fprintf(&b, `<gpxa:dist>%.2f</gpxa:dist>`, cumDist)
	fmt.Fprintf(&b, `<gpxa:grade>%.6f</gpxa:grade>`, grade)
	b.WriteString(`</gpxa:TrackPointMetrics>`)

	// Biometrics (Garmin TrackPointExtension format)
	if tp.HeartRate != nil || tp.Cadence != nil || tp.Power != nil || tp.Temperature != nil {
		b.WriteString(`<gpxtpx:TrackPointExtension xmlns:gpxtpx="http://www.garmin.com/xmlschemas/TrackPointExtension/v1">`)
		if tp.HeartRate != nil {
			fmt.Fprintf(&b, `<gpxtpx:hr>%d</gpxtpx:hr>`, *tp.HeartRate)
		}
		if tp.Cadence != nil {
			fmt.Fprintf(&b, `<gpxtpx:cad>%d</gpxtpx:cad>`, *tp.Cadence)
		}
		if tp.Temperature != nil {
			fmt.Fprintf(&b, `<gpxtpx:atemp>%.1f</gpxtpx:atemp>`, *tp.Temperature)
		}
		b.WriteString(`</gpxtpx:TrackPointExtension>`)
		if tp.Power != nil {
			fmt.Fprintf(&b, `<power>%d</power>`, *tp.Power)
		}
	}

	return b.String()
}
