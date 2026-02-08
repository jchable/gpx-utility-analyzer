package stats

import (
	"math"
	"time"

	"github.com/jchable/gpx-utility-analyzer/cli/internal/gpx"
)

// HeartRateZone represents a named HR zone with time spent in it.
type HeartRateZone struct {
	Name       string
	MinPercent int
	MaxPercent int
	Duration   time.Duration
}

// HeartRateResult holds computed heart rate statistics.
type HeartRateResult struct {
	Avg   float64
	Max   int
	Min   int
	Zones []HeartRateZone
}

// PowerResult holds computed power statistics.
type PowerResult struct {
	Avg             float64
	Max             int
	NormalizedPower float64
}

// CadenceResult holds computed cadence statistics.
type CadenceResult struct {
	Avg float64
	Max int
}

// TemperatureResult holds computed temperature statistics.
type TemperatureResult struct {
	Avg float64
	Min float64
	Max float64
}

// BiometricsResult aggregates all biometric results.
// Nil sub-results indicate no data was present for that metric.
type BiometricsResult struct {
	HeartRate   *HeartRateResult
	Power       *PowerResult
	Cadence     *CadenceResult
	Temperature *TemperatureResult
}

// BiometricsConfig holds parameters for biometric computation.
type BiometricsConfig struct {
	MaxHR int // max heart rate for zone calculation (0 = skip zones)
}

// ComputeBiometrics calculates biometric statistics from trackpoints.
func ComputeBiometrics(points []gpx.TrackPoint, cfg BiometricsConfig) BiometricsResult {
	return BiometricsResult{
		HeartRate:   computeHeartRate(points, cfg.MaxHR),
		Power:       computePower(points),
		Cadence:     computeCadence(points),
		Temperature: computeTemperature(points),
	}
}

func computeHeartRate(points []gpx.TrackPoint, maxHR int) *HeartRateResult {
	var sum float64
	var count int
	hrMax := 0
	hrMin := math.MaxInt

	for _, p := range points {
		if p.HeartRate == nil {
			continue
		}
		hr := *p.HeartRate
		sum += float64(hr)
		count++
		if hr > hrMax {
			hrMax = hr
		}
		if hr < hrMin {
			hrMin = hr
		}
	}

	if count == 0 {
		return nil
	}

	result := &HeartRateResult{
		Avg: sum / float64(count),
		Max: hrMax,
		Min: hrMin,
	}

	if maxHR > 0 {
		result.Zones = computeHRZones(points, maxHR)
	}

	return result
}

func computeHRZones(points []gpx.TrackPoint, maxHR int) []HeartRateZone {
	zones := []HeartRateZone{
		{Name: "Z1 (Recovery)", MinPercent: 50, MaxPercent: 60},
		{Name: "Z2 (Endurance)", MinPercent: 60, MaxPercent: 70},
		{Name: "Z3 (Tempo)", MinPercent: 70, MaxPercent: 80},
		{Name: "Z4 (Threshold)", MinPercent: 80, MaxPercent: 90},
		{Name: "Z5 (VO2 Max)", MinPercent: 90, MaxPercent: 100},
	}

	maxF := float64(maxHR)
	for i := 1; i < len(points); i++ {
		if points[i].HeartRate == nil {
			continue
		}
		hr := float64(*points[i].HeartRate)
		pct := (hr / maxF) * 100
		dt := points[i].Time.Sub(points[i-1].Time)
		if dt <= 0 {
			continue
		}

		for z := range zones {
			lo := float64(zones[z].MinPercent)
			hi := float64(zones[z].MaxPercent)
			if z == len(zones)-1 {
				// Z5 is open-ended: 90%+
				if pct >= lo {
					zones[z].Duration += dt
				}
			} else {
				if pct >= lo && pct < hi {
					zones[z].Duration += dt
				}
			}
		}
	}

	return zones
}

func computePower(points []gpx.TrackPoint) *PowerResult {
	var sum float64
	var count int
	pMax := 0

	for _, p := range points {
		if p.Power == nil {
			continue
		}
		pw := *p.Power
		sum += float64(pw)
		count++
		if pw > pMax {
			pMax = pw
		}
	}

	if count == 0 {
		return nil
	}

	return &PowerResult{
		Avg:             sum / float64(count),
		Max:             pMax,
		NormalizedPower: computeNormalizedPower(points),
	}
}

func computeNormalizedPower(points []gpx.TrackPoint) float64 {
	type pwSample struct {
		t time.Time
		w float64
	}
	var samples []pwSample
	for _, p := range points {
		if p.Power == nil || p.Time.IsZero() {
			continue
		}
		samples = append(samples, pwSample{t: p.Time, w: float64(*p.Power)})
	}
	if len(samples) < 2 {
		return 0
	}

	const window = 30 * time.Second
	var fourthPowerSum float64
	var fourthPowerCount int

	for i, s := range samples {
		var windowSum float64
		var windowCount int
		for j := i; j >= 0; j-- {
			if s.t.Sub(samples[j].t) > window {
				break
			}
			windowSum += samples[j].w
			windowCount++
		}
		if windowCount == 0 {
			continue
		}
		avg := windowSum / float64(windowCount)
		fourthPowerSum += math.Pow(avg, 4)
		fourthPowerCount++
	}

	if fourthPowerCount == 0 {
		return 0
	}

	return math.Pow(fourthPowerSum/float64(fourthPowerCount), 0.25)
}

func computeCadence(points []gpx.TrackPoint) *CadenceResult {
	var sum float64
	var count, cMax int

	for _, p := range points {
		if p.Cadence == nil {
			continue
		}
		c := *p.Cadence
		sum += float64(c)
		count++
		if c > cMax {
			cMax = c
		}
	}

	if count == 0 {
		return nil
	}

	return &CadenceResult{
		Avg: sum / float64(count),
		Max: cMax,
	}
}

func computeTemperature(points []gpx.TrackPoint) *TemperatureResult {
	var sum float64
	tMax := -math.MaxFloat64
	tMin := math.MaxFloat64
	var count int

	for _, p := range points {
		if p.Temperature == nil {
			continue
		}
		temp := *p.Temperature
		sum += temp
		count++
		if temp > tMax {
			tMax = temp
		}
		if temp < tMin {
			tMin = temp
		}
	}

	if count == 0 {
		return nil
	}

	return &TemperatureResult{
		Avg: sum / float64(count),
		Min: tMin,
		Max: tMax,
	}
}
