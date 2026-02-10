package benchmark

import (
	"fmt"
	"strings"

	"github.com/jchable/gpx-utility-analyzer/cli/internal/elevation"
	"github.com/jchable/gpx-utility-analyzer/cli/internal/stats"
)

// Axis represents a configuration dimension to vary.
type Axis string

const (
	AxisPreset         Axis = "preset"
	AxisElevAlgo       Axis = "elev-algo"
	AxisElevSmoothing  Axis = "elev-smoothing"
	AxisTrackSmoothing Axis = "track-smoothing"
	AxisDEM            Axis = "dem"
	AxisElevParams     Axis = "elev-params"
)

// AllAxes returns all recognized axis names.
func AllAxes() []Axis {
	return []Axis{
		AxisPreset, AxisElevAlgo, AxisElevSmoothing,
		AxisTrackSmoothing, AxisDEM, AxisElevParams,
	}
}

// ParseAxes parses a comma-separated list of axis names.
func ParseAxes(s string) ([]Axis, error) {
	if s == "" {
		return nil, nil
	}
	valid := make(map[Axis]bool)
	for _, a := range AllAxes() {
		valid[a] = true
	}
	parts := strings.Split(s, ",")
	axes := make([]Axis, 0, len(parts))
	for _, p := range parts {
		a := Axis(strings.TrimSpace(p))
		if !valid[a] {
			return nil, fmt.Errorf("unknown axis %q (valid: %s)", a, strings.Join(axisNames(), ", "))
		}
		axes = append(axes, a)
	}
	return axes, nil
}

func axisNames() []string {
	all := AllAxes()
	names := make([]string, len(all))
	for i, a := range all {
		names[i] = string(a)
	}
	return names
}

// Combination represents one specific configuration to benchmark.
type Combination struct {
	Preset         string
	ElevAlgo       stats.ElevationAlgo
	ElevSmoothing  elevation.SmoothingLevel
	TrackSmoothing elevation.TrackSmoothingLevel
	UseDEM         bool
	Threshold      float64 // for threshold algo
	DPEpsilon      float64 // for douglas-peucker algo
	SegMinLen      float64 // for segments algo
	SegMaxDev      float64 // for segments algo
}

// ParamsLabel returns a short label for the algorithm-specific parameters.
func (c Combination) ParamsLabel() string {
	switch c.ElevAlgo {
	case stats.AlgoDouglasPeucker:
		return fmt.Sprintf("e=%.1f", c.DPEpsilon)
	case stats.AlgoSegments:
		return fmt.Sprintf("l=%.0f/d=%.1f", c.SegMinLen, c.SegMaxDev)
	default:
		return fmt.Sprintf("t=%.1f", c.Threshold)
	}
}

// Label returns a unique key for deduplication.
func (c Combination) Label() string {
	return fmt.Sprintf("%s|%s|%s|%s|%v|%s",
		c.Preset, c.ElevAlgo, c.ElevSmoothing, c.TrackSmoothing, c.UseDEM, c.ParamsLabel())
}

// MatrixConfig controls which values are used for each axis.
type MatrixConfig struct {
	Presets         []string
	ElevAlgos       []stats.ElevationAlgo
	ElevSmoothings  []elevation.SmoothingLevel
	TrackSmoothings []elevation.TrackSmoothingLevel
	DEMValues       []bool
	Thresholds      []float64
	DPEpsilons      []float64
	SegMinLens      []float64
	SegMaxDevs      []float64
}

// DefaultBase returns the default base combination used for reduced mode.
func DefaultBase() Combination {
	return Combination{
		Preset:         stats.PresetHiking,
		ElevAlgo:       stats.AlgoThreshold,
		ElevSmoothing:  elevation.SmoothMedium,
		TrackSmoothing: elevation.TrackSmoothNone,
		UseDEM:         true,
		Threshold:      2.0,
		DPEpsilon:      3.0,
		SegMinLen:      200.0,
		SegMaxDev:      2.0,
	}
}

// FullMatrixConfig returns a config with all possible values for all axes.
func FullMatrixConfig() MatrixConfig {
	return MatrixConfig{
		Presets:         []string{stats.PresetHiking, stats.PresetTrail, stats.PresetCycling},
		ElevAlgos:       []stats.ElevationAlgo{stats.AlgoThreshold, stats.AlgoDouglasPeucker, stats.AlgoSegments},
		ElevSmoothings:  []elevation.SmoothingLevel{elevation.SmoothNone, elevation.SmoothLight, elevation.SmoothMedium, elevation.SmoothHeavy},
		TrackSmoothings: []elevation.TrackSmoothingLevel{elevation.TrackSmoothNone, elevation.TrackSmoothLight, elevation.TrackSmoothMedium, elevation.TrackSmoothHeavy},
		DEMValues:       []bool{true, false},
		Thresholds:      []float64{1.0, 2.0, 3.0, 5.0},
		DPEpsilons:      []float64{1.5, 3.0, 5.0},
		SegMinLens:      []float64{100, 200, 400},
		SegMaxDevs:      []float64{1.0, 2.0},
	}
}

// GenerateCombinations produces all valid combinations from the matrix config.
// Elevation params are coupled to their matching algorithm.
func GenerateCombinations(cfg MatrixConfig) []Combination {
	var combos []Combination

	for _, preset := range cfg.Presets {
		for _, eSmooth := range cfg.ElevSmoothings {
			for _, tSmooth := range cfg.TrackSmoothings {
				for _, useDEM := range cfg.DEMValues {
					for _, algo := range cfg.ElevAlgos {
						combos = append(combos, algoCombinations(preset, algo, eSmooth, tSmooth, useDEM, cfg)...)
					}
				}
			}
		}
	}

	return combos
}

// algoCombinations generates combinations for one algorithm, varying its specific params.
func algoCombinations(preset string, algo stats.ElevationAlgo, eSmooth elevation.SmoothingLevel, tSmooth elevation.TrackSmoothingLevel, useDEM bool, cfg MatrixConfig) []Combination {
	base := Combination{
		Preset:         preset,
		ElevAlgo:       algo,
		ElevSmoothing:  eSmooth,
		TrackSmoothing: tSmooth,
		UseDEM:         useDEM,
		Threshold:      2.0,
		DPEpsilon:      3.0,
		SegMinLen:      200.0,
		SegMaxDev:      2.0,
	}

	var combos []Combination

	switch algo {
	case stats.AlgoThreshold:
		for _, t := range cfg.Thresholds {
			c := base
			c.Threshold = t
			combos = append(combos, c)
		}
	case stats.AlgoDouglasPeucker:
		for _, e := range cfg.DPEpsilons {
			c := base
			c.DPEpsilon = e
			combos = append(combos, c)
		}
	case stats.AlgoSegments:
		for _, l := range cfg.SegMinLens {
			for _, d := range cfg.SegMaxDevs {
				c := base
				c.SegMinLen = l
				c.SegMaxDev = d
				combos = append(combos, c)
			}
		}
	}

	return combos
}

// ReducedCombinations generates a one-axis-at-a-time sensitivity analysis.
// For each axis, all its values are tested while other axes remain at their defaults.
// Duplicates are deduplicated by Label().
func ReducedCombinations() []Combination {
	base := DefaultBase()
	cfg := FullMatrixConfig()
	seen := make(map[string]bool)
	var combos []Combination

	add := func(c Combination) {
		key := c.Label()
		if !seen[key] {
			seen[key] = true
			combos = append(combos, c)
		}
	}

	// Vary presets
	for _, p := range cfg.Presets {
		c := base
		c.Preset = p
		add(c)
	}

	// Vary elevation algorithms (with their default params)
	for _, algo := range cfg.ElevAlgos {
		c := base
		c.ElevAlgo = algo
		add(c)
	}

	// Vary elevation smoothing
	for _, s := range cfg.ElevSmoothings {
		c := base
		c.ElevSmoothing = s
		add(c)
	}

	// Vary track smoothing
	for _, s := range cfg.TrackSmoothings {
		c := base
		c.TrackSmoothing = s
		add(c)
	}

	// Vary DEM
	for _, d := range cfg.DEMValues {
		c := base
		c.UseDEM = d
		add(c)
	}

	// Vary elevation params (algo-specific)
	for _, t := range cfg.Thresholds {
		c := base
		c.ElevAlgo = stats.AlgoThreshold
		c.Threshold = t
		add(c)
	}
	for _, e := range cfg.DPEpsilons {
		c := base
		c.ElevAlgo = stats.AlgoDouglasPeucker
		c.DPEpsilon = e
		add(c)
	}
	for _, l := range cfg.SegMinLens {
		for _, d := range cfg.SegMaxDevs {
			c := base
			c.ElevAlgo = stats.AlgoSegments
			c.SegMinLen = l
			c.SegMaxDev = d
			add(c)
		}
	}

	return combos
}

// VaryCombinations generates combinations varying only the specified axes.
// Other axes remain at their default values.
func VaryCombinations(axes []Axis) []Combination {
	base := DefaultBase()
	cfg := FullMatrixConfig()

	axisSet := make(map[Axis]bool)
	for _, a := range axes {
		axisSet[a] = true
	}

	// Build per-axis value lists (single default if not varied)
	presets := []string{base.Preset}
	if axisSet[AxisPreset] {
		presets = cfg.Presets
	}

	elevAlgos := []stats.ElevationAlgo{base.ElevAlgo}
	if axisSet[AxisElevAlgo] || axisSet[AxisElevParams] {
		elevAlgos = cfg.ElevAlgos
	}

	elevSmoothings := []elevation.SmoothingLevel{base.ElevSmoothing}
	if axisSet[AxisElevSmoothing] {
		elevSmoothings = cfg.ElevSmoothings
	}

	trackSmoothings := []elevation.TrackSmoothingLevel{base.TrackSmoothing}
	if axisSet[AxisTrackSmoothing] {
		trackSmoothings = cfg.TrackSmoothings
	}

	demValues := []bool{base.UseDEM}
	if axisSet[AxisDEM] {
		demValues = cfg.DEMValues
	}

	// For params: if elev-params is varied, use full param sets; otherwise single default
	varyParams := axisSet[AxisElevParams]

	paramCfg := cfg
	if !varyParams {
		paramCfg.Thresholds = []float64{base.Threshold}
		paramCfg.DPEpsilons = []float64{base.DPEpsilon}
		paramCfg.SegMinLens = []float64{base.SegMinLen}
		paramCfg.SegMaxDevs = []float64{base.SegMaxDev}
	}

	matrixCfg := MatrixConfig{
		Presets:         presets,
		ElevAlgos:       elevAlgos,
		ElevSmoothings:  elevSmoothings,
		TrackSmoothings: trackSmoothings,
		DEMValues:       demValues,
		Thresholds:      paramCfg.Thresholds,
		DPEpsilons:      paramCfg.DPEpsilons,
		SegMinLens:      paramCfg.SegMinLens,
		SegMaxDevs:      paramCfg.SegMaxDevs,
	}

	return GenerateCombinations(matrixCfg)
}
