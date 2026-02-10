package gpx

import (
	"bytes"
	"strings"
	"testing"
	"time"
)

func TestNewGPXFromPoints_Basic(t *testing.T) {
	points := []TrackPoint{
		{Lat: 45.0, Lon: 6.0, Ele: 100, Time: time.Date(2025, 1, 1, 8, 0, 0, 0, time.UTC)},
		{Lat: 45.1, Lon: 6.1, Ele: 200, Time: time.Date(2025, 1, 1, 8, 10, 0, 0, time.UTC)},
	}

	g := NewGPXFromPoints(points, "test-track")
	var buf bytes.Buffer
	if err := Write(g, &buf); err != nil {
		t.Fatal(err)
	}

	xml := buf.String()
	if !strings.Contains(xml, `lat="45"`) {
		t.Error("expected lat attribute")
	}
	if !strings.Contains(xml, `<name>test-track</name>`) {
		t.Error("expected track name")
	}
	// Basic export should NOT contain enriched extensions
	if strings.Contains(xml, "gpxa:") {
		t.Error("basic export should not contain gpxa extensions")
	}
}

func TestNewEnrichedGPXFromPoints_Extensions(t *testing.T) {
	hr := 145
	cad := 85
	power := 250
	temp := 22.5

	points := []TrackPoint{
		{
			Lat: 45.0, Lon: 6.0, Ele: 100,
			Time:         time.Date(2025, 1, 1, 8, 0, 0, 0, time.UTC),
			CalcSpeed:    0,
			DistFromPrev: 0,
			HeartRate:    &hr,
			Cadence:      &cad,
			Power:        &power,
			Temperature:  &temp,
		},
		{
			Lat: 45.001, Lon: 6.001, Ele: 110,
			Time:         time.Date(2025, 1, 1, 8, 0, 30, 0, time.UTC),
			CalcSpeed:    4.2,
			DistFromPrev: 126.0,
			HeartRate:    &hr,
			Cadence:      &cad,
		},
	}

	g := NewEnrichedGPXFromPoints(points, "enriched-track")
	var buf bytes.Buffer
	if err := Write(g, &buf); err != nil {
		t.Fatal(err)
	}

	xml := buf.String()

	// Check computed metrics extensions
	if !strings.Contains(xml, "gpxa:TrackPointMetrics") {
		t.Error("expected gpxa:TrackPointMetrics extension")
	}
	if !strings.Contains(xml, "<gpxa:speed>4.2000</gpxa:speed>") {
		t.Error("expected speed extension with CalcSpeed value")
	}
	if !strings.Contains(xml, "<gpxa:dist>126.00</gpxa:dist>") {
		t.Error("expected cumulative distance extension")
	}
	if !strings.Contains(xml, "<gpxa:grade>") {
		t.Error("expected grade extension")
	}

	// Check biometrics extensions
	if !strings.Contains(xml, "gpxtpx:TrackPointExtension") {
		t.Error("expected Garmin TrackPointExtension")
	}
	if !strings.Contains(xml, "<gpxtpx:hr>145</gpxtpx:hr>") {
		t.Error("expected heart rate extension")
	}
	if !strings.Contains(xml, "<gpxtpx:cad>85</gpxtpx:cad>") {
		t.Error("expected cadence extension")
	}
	if !strings.Contains(xml, "<power>250</power>") {
		t.Error("expected power extension on first point")
	}
	if !strings.Contains(xml, "<gpxtpx:atemp>22.5</gpxtpx:atemp>") {
		t.Error("expected temperature extension on first point")
	}
}

func TestNewEnrichedGPXFromPoints_CumulativeDistance(t *testing.T) {
	points := []TrackPoint{
		{Lat: 45.0, Lon: 6.0, Ele: 100, DistFromPrev: 0},
		{Lat: 45.1, Lon: 6.1, Ele: 110, DistFromPrev: 100},
		{Lat: 45.2, Lon: 6.2, Ele: 120, DistFromPrev: 200},
	}

	g := NewEnrichedGPXFromPoints(points, "dist-test")
	var buf bytes.Buffer
	if err := Write(g, &buf); err != nil {
		t.Fatal(err)
	}

	xml := buf.String()

	// First point: cumDist = 0
	if !strings.Contains(xml, "<gpxa:dist>0.00</gpxa:dist>") {
		t.Error("first point should have cumulative distance 0")
	}
	// Second point: cumDist = 100
	if !strings.Contains(xml, "<gpxa:dist>100.00</gpxa:dist>") {
		t.Error("second point should have cumulative distance 100")
	}
	// Third point: cumDist = 300
	if !strings.Contains(xml, "<gpxa:dist>300.00</gpxa:dist>") {
		t.Error("third point should have cumulative distance 300")
	}
}

func TestNewEnrichedGPXFromPoints_GradeComputation(t *testing.T) {
	points := []TrackPoint{
		{Lat: 45.0, Lon: 6.0, Ele: 100, DistFromPrev: 0},
		{Lat: 45.1, Lon: 6.1, Ele: 110, DistFromPrev: 100}, // grade = 10/100 = 0.1
	}

	g := NewEnrichedGPXFromPoints(points, "grade-test")
	var buf bytes.Buffer
	if err := Write(g, &buf); err != nil {
		t.Fatal(err)
	}

	xml := buf.String()

	// Grade for second point: (110-100)/100 = 0.1
	if !strings.Contains(xml, "<gpxa:grade>0.100000</gpxa:grade>") {
		t.Errorf("expected grade 0.100000, got: %s", xml)
	}
}

func TestNewEnrichedGPXFromPoints_NoBiometrics(t *testing.T) {
	points := []TrackPoint{
		{Lat: 45.0, Lon: 6.0, Ele: 100, CalcSpeed: 3.0, DistFromPrev: 0},
	}

	g := NewEnrichedGPXFromPoints(points, "no-bio")
	var buf bytes.Buffer
	if err := Write(g, &buf); err != nil {
		t.Fatal(err)
	}

	xml := buf.String()

	// Should have computed metrics but NOT biometrics
	if !strings.Contains(xml, "gpxa:TrackPointMetrics") {
		t.Error("expected computed metrics extension")
	}
	if strings.Contains(xml, "gpxtpx:TrackPointExtension") {
		t.Error("should not contain biometrics when none present")
	}
}
