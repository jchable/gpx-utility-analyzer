package stats

import (
	"math"
	"testing"
	"time"

	"github.com/jchable/gpx-utility-analyzer/cli/internal/gpx"
)

func intPtr(v int) *int         { return &v }
func floatPtr(v float64) *float64 { return &v }

func makeBioTime(sec int) time.Time {
	return time.Date(2024, 1, 1, 10, 0, sec, 0, time.UTC)
}

func TestComputeBiometrics_NoData(t *testing.T) {
	points := []gpx.TrackPoint{
		{Lat: 48.0, Lon: 2.0, Time: makeBioTime(0)},
		{Lat: 48.1, Lon: 2.1, Time: makeBioTime(60)},
	}

	result := ComputeBiometrics(points, BiometricsConfig{})

	if result.HeartRate != nil {
		t.Error("HeartRate should be nil when no HR data")
	}
	if result.Power != nil {
		t.Error("Power should be nil when no power data")
	}
	if result.Cadence != nil {
		t.Error("Cadence should be nil when no cadence data")
	}
	if result.Temperature != nil {
		t.Error("Temperature should be nil when no temperature data")
	}
}

func TestComputeHeartRate_AvgMaxMin(t *testing.T) {
	points := []gpx.TrackPoint{
		{Time: makeBioTime(0), HeartRate: intPtr(120)},
		{Time: makeBioTime(60), HeartRate: intPtr(150)},
		{Time: makeBioTime(120), HeartRate: intPtr(180)},
	}

	result := ComputeBiometrics(points, BiometricsConfig{})

	hr := result.HeartRate
	if hr == nil {
		t.Fatal("HeartRate should not be nil")
	}
	if hr.Avg != 150 {
		t.Errorf("Avg: got %f, want 150", hr.Avg)
	}
	if hr.Max != 180 {
		t.Errorf("Max: got %d, want 180", hr.Max)
	}
	if hr.Min != 120 {
		t.Errorf("Min: got %d, want 120", hr.Min)
	}
	if hr.Zones != nil {
		t.Error("Zones should be nil when MaxHR is 0")
	}
}

func TestComputeHRZones(t *testing.T) {
	maxHR := 200
	// Point 0: HR=100 (50% of 200) -> start of Z1
	// Point 1: HR=100 (50%) -> 60s in Z1
	// Point 2: HR=140 (70%) -> 60s in Z3
	// Point 3: HR=180 (90%) -> 60s in Z5
	points := []gpx.TrackPoint{
		{Time: makeBioTime(0), HeartRate: intPtr(100)},
		{Time: makeBioTime(60), HeartRate: intPtr(100)},
		{Time: makeBioTime(120), HeartRate: intPtr(140)},
		{Time: makeBioTime(180), HeartRate: intPtr(180)},
	}

	result := ComputeBiometrics(points, BiometricsConfig{MaxHR: maxHR})

	hr := result.HeartRate
	if hr == nil {
		t.Fatal("HeartRate should not be nil")
	}
	if len(hr.Zones) != 5 {
		t.Fatalf("expected 5 zones, got %d", len(hr.Zones))
	}

	// Z1 (50-60%): HR=100 is exactly 50%, so 60s
	if hr.Zones[0].Duration != 60*time.Second {
		t.Errorf("Z1 duration: got %v, want 1m0s", hr.Zones[0].Duration)
	}
	// Z3 (70-80%): HR=140 is exactly 70%, so 60s
	if hr.Zones[2].Duration != 60*time.Second {
		t.Errorf("Z3 duration: got %v, want 1m0s", hr.Zones[2].Duration)
	}
	// Z5 (90%+): HR=180 is exactly 90%, so 60s
	if hr.Zones[4].Duration != 60*time.Second {
		t.Errorf("Z5 duration: got %v, want 1m0s", hr.Zones[4].Duration)
	}
}

func TestComputePower_AvgMax(t *testing.T) {
	points := []gpx.TrackPoint{
		{Time: makeBioTime(0), Power: intPtr(200)},
		{Time: makeBioTime(60), Power: intPtr(250)},
		{Time: makeBioTime(120), Power: intPtr(300)},
	}

	result := ComputeBiometrics(points, BiometricsConfig{})

	pw := result.Power
	if pw == nil {
		t.Fatal("Power should not be nil")
	}
	if pw.Avg != 250 {
		t.Errorf("Avg: got %f, want 250", pw.Avg)
	}
	if pw.Max != 300 {
		t.Errorf("Max: got %d, want 300", pw.Max)
	}
	// NP should be computed (>0 for valid data)
	if pw.NormalizedPower <= 0 {
		t.Error("NormalizedPower should be > 0")
	}
}

func TestComputeNormalizedPower_ConstantPower(t *testing.T) {
	// With constant power, NP should equal the power value.
	var points []gpx.TrackPoint
	for i := 0; i < 60; i++ {
		points = append(points, gpx.TrackPoint{
			Time:  makeBioTime(i),
			Power: intPtr(200),
		})
	}

	result := ComputeBiometrics(points, BiometricsConfig{})

	pw := result.Power
	if pw == nil {
		t.Fatal("Power should not be nil")
	}
	if math.Abs(pw.NormalizedPower-200) > 1 {
		t.Errorf("NP for constant power: got %f, want ~200", pw.NormalizedPower)
	}
}

func TestComputeCadence(t *testing.T) {
	points := []gpx.TrackPoint{
		{Cadence: intPtr(80)},
		{Cadence: intPtr(90)},
		{Cadence: intPtr(100)},
	}

	result := ComputeBiometrics(points, BiometricsConfig{})

	cad := result.Cadence
	if cad == nil {
		t.Fatal("Cadence should not be nil")
	}
	if cad.Avg != 90 {
		t.Errorf("Avg: got %f, want 90", cad.Avg)
	}
	if cad.Max != 100 {
		t.Errorf("Max: got %d, want 100", cad.Max)
	}
}

func TestComputeTemperature(t *testing.T) {
	points := []gpx.TrackPoint{
		{Temperature: floatPtr(18.0)},
		{Temperature: floatPtr(22.0)},
		{Temperature: floatPtr(20.0)},
	}

	result := ComputeBiometrics(points, BiometricsConfig{})

	temp := result.Temperature
	if temp == nil {
		t.Fatal("Temperature should not be nil")
	}
	if temp.Avg != 20 {
		t.Errorf("Avg: got %f, want 20", temp.Avg)
	}
	if temp.Min != 18 {
		t.Errorf("Min: got %f, want 18", temp.Min)
	}
	if temp.Max != 22 {
		t.Errorf("Max: got %f, want 22", temp.Max)
	}
}
