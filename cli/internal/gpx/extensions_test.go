package gpx

import "testing"

func intPtr(v int) *int         { return &v }
func floatPtr(v float64) *float64 { return &v }

func TestParseExtensions_GarminV2(t *testing.T) {
	xml := `<gpxtpx:TrackPointExtension xmlns:gpxtpx="http://www.garmin.com/xmlschemas/TrackPointExtension/v2">
		<gpxtpx:hr>145</gpxtpx:hr>
		<gpxtpx:cad>90</gpxtpx:cad>
		<gpxtpx:atemp>22.5</gpxtpx:atemp>
	</gpxtpx:TrackPointExtension>
	<power>250</power>`

	ext := ParseExtensions(xml)

	if ext.HeartRate == nil || *ext.HeartRate != 145 {
		t.Errorf("HeartRate: got %v, want 145", ext.HeartRate)
	}
	if ext.Cadence == nil || *ext.Cadence != 90 {
		t.Errorf("Cadence: got %v, want 90", ext.Cadence)
	}
	if ext.Temperature == nil || *ext.Temperature != 22.5 {
		t.Errorf("Temperature: got %v, want 22.5", ext.Temperature)
	}
	if ext.Power == nil || *ext.Power != 250 {
		t.Errorf("Power: got %v, want 250", ext.Power)
	}
}

func TestParseExtensions_GarminV1(t *testing.T) {
	xml := `<ns3:TrackPointExtension xmlns:ns3="http://www.garmin.com/xmlschemas/TrackPointExtension/v1">
		<ns3:hr>130</ns3:hr>
		<ns3:cad>85</ns3:cad>
		<ns3:atemp>18.0</ns3:atemp>
	</ns3:TrackPointExtension>`

	ext := ParseExtensions(xml)

	if ext.HeartRate == nil || *ext.HeartRate != 130 {
		t.Errorf("HeartRate: got %v, want 130", ext.HeartRate)
	}
	if ext.Cadence == nil || *ext.Cadence != 85 {
		t.Errorf("Cadence: got %v, want 85", ext.Cadence)
	}
	if ext.Temperature == nil || *ext.Temperature != 18.0 {
		t.Errorf("Temperature: got %v, want 18.0", ext.Temperature)
	}
}

func TestParseExtensions_PowerOnly(t *testing.T) {
	xml := `<power>300</power>`

	ext := ParseExtensions(xml)

	if ext.Power == nil || *ext.Power != 300 {
		t.Errorf("Power: got %v, want 300", ext.Power)
	}
	if ext.HeartRate != nil {
		t.Errorf("HeartRate: expected nil, got %v", *ext.HeartRate)
	}
}

func TestParseExtensions_Empty(t *testing.T) {
	ext := ParseExtensions("")
	if ext.HeartRate != nil || ext.Cadence != nil || ext.Power != nil || ext.Temperature != nil {
		t.Error("expected all nil for empty extensions")
	}
}

func TestParseExtensions_Partial(t *testing.T) {
	xml := `<gpxtpx:TrackPointExtension xmlns:gpxtpx="http://www.garmin.com/xmlschemas/TrackPointExtension/v2">
		<gpxtpx:hr>140</gpxtpx:hr>
	</gpxtpx:TrackPointExtension>`

	ext := ParseExtensions(xml)

	if ext.HeartRate == nil || *ext.HeartRate != 140 {
		t.Errorf("HeartRate: got %v, want 140", ext.HeartRate)
	}
	if ext.Cadence != nil {
		t.Errorf("Cadence: expected nil, got %v", *ext.Cadence)
	}
	if ext.Temperature != nil {
		t.Errorf("Temperature: expected nil, got %v", *ext.Temperature)
	}
}
