package stats

import (
	"time"

	"github.com/jchable/gpx-utility-analyzer/cli/internal/gpx"
)

// Stop represents a detected stop period.
type Stop struct {
	StartTime time.Time
	EndTime   time.Time
	Duration  time.Duration
	Lat       float64 // centroid latitude
	Lon       float64 // centroid longitude
}

// StopConfig defines the parameters for stop detection.
type StopConfig struct {
	MaxSpeed    float64       // m/s - below this, considered not moving
	MinDuration time.Duration // minimum duration to count as a stop
	MaxDistance  float64       // meters - max displacement (start→end) for a stop; 0 = no check
}

// Preset names for stop detection.
const (
	PresetHiking  = "hiking"
	PresetTrail   = "trail"
	PresetCycling = "cycling"
)

// Presets contains predefined stop detection configurations.
// MaxSpeed is set near the GPS noise floor to avoid classifying slow uphills as stops.
// MaxDistance rejects candidate stops where the person actually covered real distance.
var Presets = map[string]StopConfig{
	PresetHiking:  {MaxSpeed: 0.2, MinDuration: 3 * time.Minute, MaxDistance: 30},
	PresetTrail:   {MaxSpeed: 0.3, MinDuration: 2 * time.Minute, MaxDistance: 50},
	PresetCycling: {MaxSpeed: 1.0, MinDuration: 30 * time.Second, MaxDistance: 100},
}

// DefaultPreset returns the default stop detection preset name.
func DefaultPreset() string {
	return PresetHiking
}

// DetectStops identifies stop periods in the given enriched trackpoints.
// Points must have CalcSpeed populated (via EnrichPoints).
func DetectStops(points []gpx.TrackPoint, cfg StopConfig) []Stop {
	if len(points) < 2 {
		return nil
	}

	var stops []Stop
	inStop := false
	var stopStart int

	for i := 1; i < len(points); i++ {
		isSlow := points[i].CalcSpeed <= cfg.MaxSpeed

		if isSlow && !inStop {
			inStop = true
			stopStart = i - 1
		} else if !isSlow && inStop {
			stop := buildStop(points[stopStart:i], cfg)
			if stop != nil {
				stops = append(stops, *stop)
			}
			inStop = false
		}
	}

	// Handle stop at end of data
	if inStop {
		stop := buildStop(points[stopStart:], cfg)
		if stop != nil {
			stops = append(stops, *stop)
		}
	}

	return stops
}

func buildStop(points []gpx.TrackPoint, cfg StopConfig) *Stop {
	if len(points) < 2 {
		return nil
	}

	duration := points[len(points)-1].Time.Sub(points[0].Time)
	if duration < cfg.MinDuration {
		return nil
	}

	// Reject if the person actually moved too far (slow uphill, not a real stop)
	if cfg.MaxDistance > 0 {
		dist := Haversine(
			points[0].Lat, points[0].Lon,
			points[len(points)-1].Lat, points[len(points)-1].Lon,
		)
		if dist > cfg.MaxDistance {
			return nil
		}
	}

	// Compute centroid
	var sumLat, sumLon float64
	for _, p := range points {
		sumLat += p.Lat
		sumLon += p.Lon
	}
	n := float64(len(points))

	return &Stop{
		StartTime: points[0].Time,
		EndTime:   points[len(points)-1].Time,
		Duration:  duration,
		Lat:       sumLat / n,
		Lon:       sumLon / n,
	}
}

// TotalStopTime returns the total duration of all stops.
func TotalStopTime(stops []Stop) time.Duration {
	var total time.Duration
	for _, s := range stops {
		total += s.Duration
	}
	return total
}

// LongestStop returns the stop with the longest duration, or nil if no stops.
func LongestStop(stops []Stop) *Stop {
	if len(stops) == 0 {
		return nil
	}
	longest := &stops[0]
	for i := 1; i < len(stops); i++ {
		if stops[i].Duration > longest.Duration {
			longest = &stops[i]
		}
	}
	return longest
}

// AvgStopDuration returns the average stop duration, or 0 if no stops.
func AvgStopDuration(stops []Stop) time.Duration {
	if len(stops) == 0 {
		return 0
	}
	return TotalStopTime(stops) / time.Duration(len(stops))
}
