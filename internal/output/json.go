package output

import (
	"encoding/json"
	"fmt"
	"io"
	"time"

	"github.com/jchable/gpx-utility-analyzer/internal/stats"
)

// JSONFormatter outputs statistics as JSON.
type JSONFormatter struct{}

// jsonSummary is the JSON-serializable version of Summary.
type jsonSummary struct {
	Filename string `json:"filename"`

	// Distance
	TotalDistance   float64 `json:"total_distance_m"`
	TotalDistance3D float64 `json:"total_distance_3d_m"`
	TotalDistanceKm float64 `json:"total_distance_km"`

	// Elevation
	ElevationGain float64 `json:"elevation_gain_m"`
	ElevationLoss float64 `json:"elevation_loss_m"`
	MaxElevation  float64 `json:"max_elevation_m"`
	MinElevation  float64 `json:"min_elevation_m"`

	// Time
	StartTime   string  `json:"start_time"`
	EndTime     string  `json:"end_time"`
	TotalTime   jsonDur `json:"total_time"`
	MovingTime  jsonDur `json:"moving_time"`
	StoppedTime jsonDur `json:"stopped_time"`

	// Speed
	AvgSpeedKmh       float64 `json:"avg_speed_kmh"`
	AvgMovingSpeedKmh float64 `json:"avg_moving_speed_kmh"`
	MaxSpeedKmh       float64 `json:"max_speed_kmh"`
	AvgPace           string  `json:"avg_pace"`
	AvgMovingPace     string  `json:"avg_moving_pace"`

	// Points
	PointCount   int     `json:"point_count"`
	SegmentCount int     `json:"segment_count"`
	PointsPerKm  float64 `json:"points_per_km"`

	// Stops
	StopCount       int        `json:"stop_count"`
	TotalStopTime   jsonDur    `json:"total_stop_time"`
	AvgStopDuration jsonDur    `json:"avg_stop_duration"`
	LongestStop     *jsonStop  `json:"longest_stop,omitempty"`
	Stops           []jsonStop `json:"stops,omitempty"`
}

type jsonDur struct {
	Display string  `json:"display"`
	Seconds float64 `json:"seconds"`
}

type jsonStop struct {
	StartTime string  `json:"start_time"`
	EndTime   string  `json:"end_time"`
	Duration  jsonDur `json:"duration"`
	Lat       float64 `json:"lat"`
	Lon       float64 `json:"lon"`
}

func (f *JSONFormatter) Format(w io.Writer, filename string, s stats.Summary, _ stats.StopConfig) error {
	js := jsonSummary{
		Filename:          filename,
		TotalDistance:     s.TotalDistance,
		TotalDistance3D:   s.TotalDistance3D,
		TotalDistanceKm:  s.TotalDistance / 1000,
		ElevationGain:    s.Elevation.Gain,
		ElevationLoss:    s.Elevation.Loss,
		MaxElevation:     s.Elevation.Max,
		MinElevation:     s.Elevation.Min,
		StartTime:        s.StartTime.Format(time.RFC3339),
		EndTime:          s.EndTime.Format(time.RFC3339),
		TotalTime:        toDur(s.TotalTime),
		MovingTime:       toDur(s.MovingTime),
		StoppedTime:      toDur(s.StoppedTime),
		AvgSpeedKmh:      s.Speed.AvgSpeed * 3.6,
		AvgMovingSpeedKmh: s.Speed.AvgMovingSpeed * 3.6,
		MaxSpeedKmh:      s.Speed.MaxSpeed * 3.6,
		AvgPace:          FormatPace(s.Speed.AvgPace),
		AvgMovingPace:    FormatPace(s.Speed.AvgMovingPace),
		PointCount:       s.PointCount,
		SegmentCount:     s.SegmentCount,
		PointsPerKm:     s.PointsPerKm,
		StopCount:        s.StopCount,
		TotalStopTime:    toDur(s.TotalStopTime),
		AvgStopDuration:  toDur(s.AvgStopDuration),
	}

	if s.LongestStop != nil {
		js.LongestStop = toJSONStop(s.LongestStop)
	}

	for _, stop := range s.Stops {
		js.Stops = append(js.Stops, *toJSONStop(&stop))
	}

	enc := json.NewEncoder(w)
	enc.SetIndent("", "  ")
	if err := enc.Encode(js); err != nil {
		return fmt.Errorf("encoding JSON: %w", err)
	}
	return nil
}

func toDur(d time.Duration) jsonDur {
	return jsonDur{
		Display: FormatDuration(d),
		Seconds: d.Seconds(),
	}
}

func toJSONStop(s *stats.Stop) *jsonStop {
	return &jsonStop{
		StartTime: s.StartTime.Format(time.RFC3339),
		EndTime:   s.EndTime.Format(time.RFC3339),
		Duration:  toDur(s.Duration),
		Lat:       s.Lat,
		Lon:       s.Lon,
	}
}
