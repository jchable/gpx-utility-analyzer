package stats

import (
	"time"

	"github.com/jchable/gpx-utility-analyzer/internal/dem"
	"github.com/jchable/gpx-utility-analyzer/internal/elevation"
	"github.com/jchable/gpx-utility-analyzer/internal/gpx"
)

// Summary holds all computed statistics for a GPX file or segment.
type Summary struct {
	// Distance
	TotalDistance   float64 // meters (2D)
	TotalDistance3D float64 // meters (3D with elevation)

	// Elevation
	Elevation ElevationResult

	// Time
	TotalTime   time.Duration
	MovingTime  time.Duration
	StoppedTime time.Duration
	StartTime   time.Time
	EndTime     time.Time

	// Speed & Pace
	Speed SpeedResult

	// Points metadata
	PointCount   int
	SegmentCount int
	PointsPerKm  float64

	// Stops
	Stops           []Stop
	StopCount       int
	TotalStopTime   time.Duration
	LongestStop     *Stop
	AvgStopDuration time.Duration
}

// ComputeConfig holds parameters for the summary computation.
type ComputeConfig struct {
	ElevationThreshold float64                  // meters, for noise filtering
	StopConfig         StopConfig               // stop detection parameters
	SmoothingLevel     elevation.SmoothingLevel // elevation smoothing preset
	DEMSource          *dem.Source              // DEM tile source (nil = disabled)
	ElevationCfg       ElevationConfig                // elevation algorithm config
	TrackSmoothing     elevation.TrackSmoothingLevel   // lat/lon smoothing before DEM
}

// DefaultConfig returns a default computation configuration using the hiking preset.
func DefaultConfig() ComputeConfig {
	return ComputeConfig{
		ElevationThreshold: 2.0,
		StopConfig:         Presets[PresetHiking],
		SmoothingLevel:     elevation.SmoothMedium,
	}
}

// Compute calculates all statistics from the given trackpoints.
func Compute(points []gpx.TrackPoint, segmentCount int, cfg ComputeConfig) Summary {
	s := Summary{
		PointCount:   len(points),
		SegmentCount: segmentCount,
	}

	if len(points) == 0 {
		return s
	}

	// Pre-process: track smoothing (lat/lon) → DEM correction → elevation smoothing
	elevation.SmoothTrack(points, cfg.TrackSmoothing)
	if cfg.DEMSource != nil {
		dem.CorrectElevations(points, cfg.DEMSource)
	}
	elevation.SmoothElevations(points, cfg.SmoothingLevel)

	// Enrich points with distance and speed
	EnrichPoints(points)

	// Distance
	for i := 1; i < len(points); i++ {
		s.TotalDistance += points[i].DistFromPrev
		s.TotalDistance3D += Distance3D(
			points[i-1].Lat, points[i-1].Lon, points[i-1].Ele,
			points[i].Lat, points[i].Lon, points[i].Ele,
		)
	}

	// Elevation — use configured algorithm (defaults to threshold)
	elevCfg := cfg.ElevationCfg
	if elevCfg.Algo == "" {
		elevCfg.Algo = AlgoThreshold
	}
	if elevCfg.Threshold == 0 {
		elevCfg.Threshold = cfg.ElevationThreshold
	}
	s.Elevation = ComputeElevationWithAlgo(points, elevCfg)

	// Time
	s.StartTime = points[0].Time
	s.EndTime = points[len(points)-1].Time
	s.TotalTime = s.EndTime.Sub(s.StartTime)

	// Stop detection
	s.Stops = DetectStops(points, cfg.StopConfig)
	s.StopCount = len(s.Stops)
	s.TotalStopTime = TotalStopTime(s.Stops)
	s.LongestStop = LongestStop(s.Stops)
	s.AvgStopDuration = AvgStopDuration(s.Stops)
	s.StoppedTime = s.TotalStopTime
	s.MovingTime = s.TotalTime - s.StoppedTime
	if s.MovingTime < 0 {
		s.MovingTime = 0
	}

	// Speed & Pace
	s.Speed = ComputeSpeed(s.TotalDistance, s.TotalTime, s.MovingTime)
	s.Speed.MaxSpeed = MaxSpeedFromPoints(points)

	// Points per km
	if s.TotalDistance > 0 {
		s.PointsPerKm = float64(s.PointCount) / (s.TotalDistance / 1000)
	}

	return s
}
