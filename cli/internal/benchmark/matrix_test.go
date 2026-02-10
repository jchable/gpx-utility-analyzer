package benchmark

import (
	"testing"

	"github.com/jchable/gpx-utility-analyzer/cli/internal/stats"
)

func TestReducedCombinations_Count(t *testing.T) {
	combos := ReducedCombinations()

	// Reduced mode varies one axis at a time from the base config.
	// Expected unique combos:
	//   presets: 3 (hiking=base, trail, cycling)
	//   elev algos: 3 (threshold=base, dp, segments)
	//   elev smoothing: 4 (none, light, medium=base, heavy)
	//   track smoothing: 4 (none=base, light, medium, heavy)
	//   DEM: 2 (true=base, false)
	//   threshold params: 4 (1.0, 2.0=base, 3.0, 5.0)
	//   dp params: 3 (1.5, 3.0, 5.0)
	//   seg params: 6 (3 minLen × 2 maxDev)
	// Base config (hiking/threshold/medium/none/DEM=true/t=2.0) is counted once.
	// Total unique after dedup: depends on overlaps.
	// The base appears in: preset=hiking, algo=threshold, eSmooth=medium, tSmooth=none, DEM=true, threshold=2.0
	// So it's deduplicated across all those axes.

	if len(combos) == 0 {
		t.Fatal("ReducedCombinations returned 0 combinations")
	}

	// Verify no duplicates
	seen := make(map[string]bool)
	for _, c := range combos {
		key := c.Label()
		if seen[key] {
			t.Errorf("duplicate combination: %s", key)
		}
		seen[key] = true
	}

	t.Logf("ReducedCombinations: %d unique combinations", len(combos))
}

func TestFullCombinations_Count(t *testing.T) {
	cfg := FullMatrixConfig()
	combos := GenerateCombinations(cfg)

	// Expected: 3 presets × (4 thresh + 3 dp + 6 seg) × 4 eSmooth × 4 tSmooth × 2 DEM
	// = 3 × 13 × 4 × 4 × 2 = 1248
	expected := 1248
	if len(combos) != expected {
		t.Errorf("FullMatrixConfig generated %d combinations, expected %d", len(combos), expected)
	}
}

func TestAlgoParamsCoupling(t *testing.T) {
	cfg := FullMatrixConfig()
	combos := GenerateCombinations(cfg)

	for _, c := range combos {
		switch c.ElevAlgo {
		case stats.AlgoThreshold:
			// Threshold should be one of the configured values
			found := false
			for _, v := range cfg.Thresholds {
				if c.Threshold == v {
					found = true
					break
				}
			}
			if !found {
				t.Errorf("threshold combo has unexpected threshold value: %f", c.Threshold)
			}
		case stats.AlgoDouglasPeucker:
			found := false
			for _, v := range cfg.DPEpsilons {
				if c.DPEpsilon == v {
					found = true
					break
				}
			}
			if !found {
				t.Errorf("dp combo has unexpected epsilon value: %f", c.DPEpsilon)
			}
		case stats.AlgoSegments:
			foundLen := false
			for _, v := range cfg.SegMinLens {
				if c.SegMinLen == v {
					foundLen = true
					break
				}
			}
			foundDev := false
			for _, v := range cfg.SegMaxDevs {
				if c.SegMaxDev == v {
					foundDev = true
					break
				}
			}
			if !foundLen {
				t.Errorf("segments combo has unexpected minLen: %f", c.SegMinLen)
			}
			if !foundDev {
				t.Errorf("segments combo has unexpected maxDev: %f", c.SegMaxDev)
			}
		}
	}
}

func TestParseAxes_Valid(t *testing.T) {
	axes, err := ParseAxes("preset,elev-algo")
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if len(axes) != 2 {
		t.Fatalf("expected 2 axes, got %d", len(axes))
	}
	if axes[0] != AxisPreset {
		t.Errorf("expected first axis to be %q, got %q", AxisPreset, axes[0])
	}
	if axes[1] != AxisElevAlgo {
		t.Errorf("expected second axis to be %q, got %q", AxisElevAlgo, axes[1])
	}
}

func TestParseAxes_Invalid(t *testing.T) {
	_, err := ParseAxes("preset,invalid-axis")
	if err == nil {
		t.Fatal("expected error for invalid axis, got nil")
	}
}

func TestParseAxes_Empty(t *testing.T) {
	axes, err := ParseAxes("")
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if axes != nil {
		t.Errorf("expected nil for empty input, got %v", axes)
	}
}

func TestVaryCombinations_SingleAxis(t *testing.T) {
	combos := VaryCombinations([]Axis{AxisPreset})

	// Should produce combinations for all presets with default elev-algo params
	// 3 presets × 1 algo (threshold with default t=2.0) × 1 eSmooth × 1 tSmooth × 1 DEM = 3
	if len(combos) != 3 {
		t.Errorf("VaryCombinations(preset) generated %d, expected 3", len(combos))
	}

	presetSet := make(map[string]bool)
	for _, c := range combos {
		presetSet[c.Preset] = true
	}
	for _, p := range []string{stats.PresetHiking, stats.PresetTrail, stats.PresetCycling} {
		if !presetSet[p] {
			t.Errorf("missing preset %q in results", p)
		}
	}
}

func TestVaryCombinations_TwoAxes(t *testing.T) {
	combos := VaryCombinations([]Axis{AxisPreset, AxisDEM})

	// 3 presets × 2 DEM × 1 algo (threshold, t=2.0) × 1 eSmooth × 1 tSmooth = 6
	if len(combos) != 6 {
		t.Errorf("VaryCombinations(preset,dem) generated %d, expected 6", len(combos))
	}
}

func TestDefaultBase(t *testing.T) {
	base := DefaultBase()
	if base.Preset != stats.PresetHiking {
		t.Errorf("default preset should be hiking, got %s", base.Preset)
	}
	if base.ElevAlgo != stats.AlgoThreshold {
		t.Errorf("default algo should be threshold, got %s", base.ElevAlgo)
	}
	if base.Threshold != 2.0 {
		t.Errorf("default threshold should be 2.0, got %f", base.Threshold)
	}
	if !base.UseDEM {
		t.Error("default DEM should be true")
	}
}

func TestCombinationParamsLabel(t *testing.T) {
	tests := []struct {
		name  string
		combo Combination
		want  string
	}{
		{
			name:  "threshold",
			combo: Combination{ElevAlgo: stats.AlgoThreshold, Threshold: 2.0},
			want:  "t=2.0",
		},
		{
			name:  "douglas-peucker",
			combo: Combination{ElevAlgo: stats.AlgoDouglasPeucker, DPEpsilon: 3.0},
			want:  "e=3.0",
		},
		{
			name:  "segments",
			combo: Combination{ElevAlgo: stats.AlgoSegments, SegMinLen: 200, SegMaxDev: 2.0},
			want:  "l=200/d=2.0",
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			got := tt.combo.ParamsLabel()
			if got != tt.want {
				t.Errorf("ParamsLabel() = %q, want %q", got, tt.want)
			}
		})
	}
}
