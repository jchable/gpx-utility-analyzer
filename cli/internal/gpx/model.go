package gpx

import (
	"encoding/xml"
	"time"
)

// GPX represents a parsed GPX document.
type GPX struct {
	XMLName xml.Name `xml:"gpx"`
	Version string   `xml:"version,attr"`
	Creator string   `xml:"creator,attr"`
	Tracks  []Track  `xml:"trk"`
}

// Track represents a GPX track.
type Track struct {
	Name     string    `xml:"name"`
	Desc     string    `xml:"desc"`
	Segments []Segment `xml:"trkseg"`
}

// Segment represents a GPX track segment.
type Segment struct {
	Points []Point `xml:"trkpt"`
}

// Point represents a GPX trackpoint with raw string time for XML unmarshalling.
type Point struct {
	Lat     float64 `xml:"lat,attr"`
	Lon     float64 `xml:"lon,attr"`
	Ele     float64 `xml:"ele"`
	RawTime string  `xml:"time"`
	Speed   float64 `xml:"speed"`
}

// TrackPoint is an enriched point used for internal computation.
type TrackPoint struct {
	Lat          float64
	Lon          float64
	Ele          float64
	Time         time.Time
	Speed        float64 // from GPX (m/s)
	CalcSpeed    float64 // computed from distance/time
	DistFromPrev float64 // meters from previous point
}

// AllPoints returns all trackpoints from all tracks and segments as a flat slice of TrackPoint.
func (g *GPX) AllPoints() ([]TrackPoint, error) {
	var points []TrackPoint
	for _, trk := range g.Tracks {
		for _, seg := range trk.Segments {
			for _, p := range seg.Points {
				tp, err := p.ToTrackPoint()
				if err != nil {
					return nil, err
				}
				points = append(points, tp)
			}
		}
	}
	return points, nil
}

// ToTrackPoint converts a raw Point to an enriched TrackPoint.
func (p *Point) ToTrackPoint() (TrackPoint, error) {
	tp := TrackPoint{
		Lat:   p.Lat,
		Lon:   p.Lon,
		Ele:   p.Ele,
		Speed: p.Speed,
	}
	if p.RawTime != "" {
		t, err := time.Parse(time.RFC3339, p.RawTime)
		if err != nil {
			return tp, err
		}
		tp.Time = t
	}
	return tp, nil
}

// SegmentCount returns the total number of segments across all tracks.
func (g *GPX) SegmentCount() int {
	count := 0
	for _, trk := range g.Tracks {
		count += len(trk.Segments)
	}
	return count
}

// PointCount returns the total number of points across all tracks and segments.
func (g *GPX) PointCount() int {
	count := 0
	for _, trk := range g.Tracks {
		for _, seg := range trk.Segments {
			count += len(seg.Points)
		}
	}
	return count
}
