package gpx

import (
	"encoding/xml"
	"strings"
)

// PointExtensions holds biometric data extracted from GPX extensions.
type PointExtensions struct {
	HeartRate   *int
	Cadence     *int
	Power       *int
	Temperature *float64
}

// garminTPE maps common fields from Garmin TrackPointExtension (v1 and v2).
type garminTPE struct {
	HR   *int     `xml:"hr"`
	Cad  *int     `xml:"cad"`
	Temp *float64 `xml:"atemp"`
}

// extensionWrapper captures known extension elements inside <extensions>.
type extensionWrapper struct {
	XMLName                xml.Name    `xml:"root"`
	TrackPointExtensions   []garminTPE `xml:"TrackPointExtension"`
	Power                  *int        `xml:"power"`
}

// rawExtensions captures the raw inner XML of <extensions> for post-processing.
type rawExtensions struct {
	InnerXML string `xml:",innerxml"`
}

// ParseExtensions extracts biometric data from the raw inner XML of a GPX <extensions> element.
// Handles Garmin TrackPointExtension v1/v2 namespaces and bare <power> elements.
func ParseExtensions(innerXML string) PointExtensions {
	trimmed := strings.TrimSpace(innerXML)
	if trimmed == "" {
		return PointExtensions{}
	}

	// Wrap in a root element with namespace declarations so the decoder resolves prefixed elements.
	wrapped := `<root xmlns:gpxtpx="http://www.garmin.com/xmlschemas/TrackPointExtension/v2"` +
		` xmlns:ns3="http://www.garmin.com/xmlschemas/TrackPointExtension/v1">` +
		trimmed + `</root>`

	var ext extensionWrapper
	_ = xml.Unmarshal([]byte(wrapped), &ext)

	var result PointExtensions
	result.Power = ext.Power

	for _, tpe := range ext.TrackPointExtensions {
		if result.HeartRate == nil && tpe.HR != nil {
			result.HeartRate = tpe.HR
		}
		if result.Cadence == nil && tpe.Cad != nil {
			result.Cadence = tpe.Cad
		}
		if result.Temperature == nil && tpe.Temp != nil {
			result.Temperature = tpe.Temp
		}
	}

	return result
}
