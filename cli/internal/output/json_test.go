package output

import (
	"bytes"
	"encoding/json"
	"testing"
	"time"

	"github.com/jchable/gpx-utility-analyzer/cli/internal/stats"
)

func makeSummary() stats.Summary {
	return stats.Summary{
		TotalDistance:   10234.5,
		TotalDistance3D: 10456.2,
		Elevation: stats.ElevationResult{
			Gain: 850.0,
			Loss: 720.0,
			Max:  1450.0,
			Min:  620.0,
		},
		StartTime:   time.Date(2024, 6, 15, 8, 0, 0, 0, time.UTC),
		EndTime:     time.Date(2024, 6, 15, 11, 30, 0, 0, time.UTC),
		TotalTime:   3*time.Hour + 30*time.Minute,
		MovingTime:  3 * time.Hour,
		StoppedTime: 30 * time.Minute,
		Speed: stats.SpeedResult{
			AvgSpeed:       0.812, // m/s
			AvgMovingSpeed: 0.948, // m/s
			MaxSpeed:       3.5,   // m/s
			AvgPace:        20*time.Minute + 31*time.Second,
			AvgMovingPace:  17*time.Minute + 35*time.Second,
		},
		PointCount:      2500,
		SegmentCount:    1,
		PointsPerKm:     244.3,
		Stops:           []stats.Stop{{StartTime: time.Date(2024, 6, 15, 9, 30, 0, 0, time.UTC), EndTime: time.Date(2024, 6, 15, 10, 0, 0, 0, time.UTC), Duration: 30 * time.Minute, Lat: 45.92, Lon: 6.87}},
		StopCount:       1,
		TotalStopTime:   30 * time.Minute,
		LongestStop:     &stats.Stop{StartTime: time.Date(2024, 6, 15, 9, 30, 0, 0, time.UTC), EndTime: time.Date(2024, 6, 15, 10, 0, 0, 0, time.UTC), Duration: 30 * time.Minute, Lat: 45.92, Lon: 6.87},
		AvgStopDuration: 30 * time.Minute,
	}
}

func formatAndParse(t *testing.T, summary stats.Summary) map[string]interface{} {
	t.Helper()
	var buf bytes.Buffer
	f := &JSONFormatter{}
	if err := f.Format(&buf, "test.gpx", summary, stats.StopConfig{}); err != nil {
		t.Fatalf("Format error: %v", err)
	}
	var result map[string]interface{}
	if err := json.Unmarshal(buf.Bytes(), &result); err != nil {
		t.Fatalf("JSON parse error: %v\nOutput: %s", err, buf.String())
	}
	return result
}

func TestJSONFormat_ProducesValidJSON(t *testing.T) {
	result := formatAndParse(t, makeSummary())
	if result["filename"] != "test.gpx" {
		t.Errorf("expected filename=test.gpx, got %v", result["filename"])
	}
}

func TestJSONFormat_AllRequiredFieldsPresent(t *testing.T) {
	result := formatAndParse(t, makeSummary())

	requiredFields := []string{
		"filename",
		"total_distance_m", "total_distance_3d_m", "total_distance_km",
		"elevation_gain_m", "elevation_loss_m", "max_elevation_m", "min_elevation_m",
		"start_time", "end_time", "total_time", "moving_time", "stopped_time",
		"avg_speed_kmh", "avg_moving_speed_kmh", "max_speed_kmh",
		"avg_pace", "avg_moving_pace",
		"point_count", "segment_count", "points_per_km",
		"stop_count", "total_stop_time", "avg_stop_duration",
	}

	for _, field := range requiredFields {
		if _, ok := result[field]; !ok {
			t.Errorf("missing required field %q in JSON output", field)
		}
	}
}

func TestJSONFormat_DistanceKmConsistentWithMeters(t *testing.T) {
	result := formatAndParse(t, makeSummary())
	distM := result["total_distance_m"].(float64)
	distKm := result["total_distance_km"].(float64)

	expected := distM / 1000
	diff := distKm - expected
	if diff < -0.01 || diff > 0.01 {
		t.Errorf("total_distance_km (%f) != total_distance_m/1000 (%f)", distKm, expected)
	}
}

func TestJSONFormat_SpeedConversion(t *testing.T) {
	result := formatAndParse(t, makeSummary())

	// MaxSpeed: 3.5 m/s → 12.6 km/h
	maxKmh := result["max_speed_kmh"].(float64)
	expectedKmh := 3.5 * 3.6
	if maxKmh < expectedKmh-0.1 || maxKmh > expectedKmh+0.1 {
		t.Errorf("max_speed_kmh expected ~%.1f, got %f", expectedKmh, maxKmh)
	}
}

func TestJSONFormat_DurationHasDisplayAndSeconds(t *testing.T) {
	result := formatAndParse(t, makeSummary())

	totalTime, ok := result["total_time"].(map[string]interface{})
	if !ok {
		t.Fatal("total_time should be an object with display and seconds")
	}
	if _, ok := totalTime["display"].(string); !ok {
		t.Error("total_time.display should be a string")
	}
	if _, ok := totalTime["seconds"].(float64); !ok {
		t.Error("total_time.seconds should be a number")
	}

	seconds := totalTime["seconds"].(float64)
	expected := (3*60 + 30) * 60.0 // 3h30m = 12600s
	if seconds != expected {
		t.Errorf("total_time.seconds expected %f, got %f", expected, seconds)
	}
}

func TestJSONFormat_StopsOmittedWhenEmpty(t *testing.T) {
	summary := makeSummary()
	summary.Stops = nil
	summary.StopCount = 0
	summary.LongestStop = nil

	result := formatAndParse(t, summary)

	if _, ok := result["stops"]; ok {
		t.Error("stops should be omitted (omitempty) when nil")
	}
	if _, ok := result["longest_stop"]; ok {
		t.Error("longest_stop should be omitted (omitempty) when nil")
	}
}

func TestJSONFormat_StopHasLatLon(t *testing.T) {
	result := formatAndParse(t, makeSummary())

	stops, ok := result["stops"].([]interface{})
	if !ok || len(stops) == 0 {
		t.Fatal("expected at least 1 stop")
	}
	stop := stops[0].(map[string]interface{})
	if _, ok := stop["lat"]; !ok {
		t.Error("stop missing lat field")
	}
	if _, ok := stop["lon"]; !ok {
		t.Error("stop missing lon field")
	}
	if _, ok := stop["start_time"]; !ok {
		t.Error("stop missing start_time field")
	}
}

func TestJSONFormat_BiometricsOmittedWhenNil(t *testing.T) {
	result := formatAndParse(t, makeSummary())

	for _, field := range []string{"heart_rate", "power", "cadence", "temperature"} {
		if _, ok := result[field]; ok {
			t.Errorf("%q should be omitted when no biometric data", field)
		}
	}
}

func TestJSONFormat_EmptySummary(t *testing.T) {
	var buf bytes.Buffer
	f := &JSONFormatter{}
	err := f.Format(&buf, "empty.gpx", stats.Summary{}, stats.StopConfig{})
	if err != nil {
		t.Fatalf("Format error on empty summary: %v", err)
	}
	var result map[string]interface{}
	if err := json.Unmarshal(buf.Bytes(), &result); err != nil {
		t.Fatalf("JSON parse error: %v", err)
	}
	if result["filename"] != "empty.gpx" {
		t.Errorf("expected filename=empty.gpx, got %v", result["filename"])
	}
}

// --- FormatDuration ---

func TestFormatDuration(t *testing.T) {
	tests := []struct {
		d    time.Duration
		want string
	}{
		{0, "0s"},
		{-1 * time.Second, "0s"},
		{45 * time.Second, "45s"},
		{5*time.Minute + 30*time.Second, "5m 30s"},
		{2*time.Hour + 15*time.Minute, "2h 15m 0s"},
		{26*time.Hour + 5*time.Minute, "1d 2h 5m 0s"},
	}
	for _, tt := range tests {
		got := FormatDuration(tt.d)
		if got != tt.want {
			t.Errorf("FormatDuration(%v) = %q, want %q", tt.d, got, tt.want)
		}
	}
}

// --- FormatPace ---

func TestFormatPace(t *testing.T) {
	tests := []struct {
		d    time.Duration
		want string
	}{
		{0, "-"},
		{-1 * time.Second, "-"},
		{6*time.Minute + 30*time.Second, "6:30 min/km"},
		{10 * time.Minute, "10:00 min/km"},
	}
	for _, tt := range tests {
		got := FormatPace(tt.d)
		if got != tt.want {
			t.Errorf("FormatPace(%v) = %q, want %q", tt.d, got, tt.want)
		}
	}
}

// --- NewFormatter ---

func TestNewFormatter_Valid(t *testing.T) {
	for _, name := range []string{"text", "json"} {
		f, err := NewFormatter(name)
		if err != nil {
			t.Errorf("NewFormatter(%q) unexpected error: %v", name, err)
		}
		if f == nil {
			t.Errorf("NewFormatter(%q) returned nil", name)
		}
	}
}

func TestNewFormatter_Invalid(t *testing.T) {
	_, err := NewFormatter("xml")
	if err == nil {
		t.Error("NewFormatter(\"xml\") should return error")
	}
}
