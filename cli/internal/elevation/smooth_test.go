package elevation

import (
	"math"
	"testing"
	"time"

	"github.com/jchable/gpx-utility-analyzer/cli/internal/gpx"
)

func TestMedianFilter_RemovesSpike(t *testing.T) {
	data := []float64{100, 100, 7583, 100, 100}
	result := medianFilter(data, 5)
	if result[2] != 100 {
		t.Errorf("expected spike at index 2 to be removed (100), got %f", result[2])
	}
}

func TestMedianFilter_PreservesMonotonic(t *testing.T) {
	data := []float64{100, 110, 120, 130, 140, 150, 160}
	result := medianFilter(data, 3)
	for i := 1; i < len(result); i++ {
		if result[i] < result[i-1] {
			t.Errorf("monotonic sequence broken at index %d: %f < %f", i, result[i], result[i-1])
		}
	}
}

func TestMedianFilter_SinglePoint(t *testing.T) {
	data := []float64{42}
	result := medianFilter(data, 5)
	if result[0] != 42 {
		t.Errorf("expected 42, got %f", result[0])
	}
}

func TestMedianFilter_WindowLargerThanData(t *testing.T) {
	data := []float64{100, 200, 150}
	result := medianFilter(data, 7)
	if len(result) != 3 {
		t.Fatalf("expected 3 results, got %d", len(result))
	}
	// Middle value should be the median of all 3: 150
	if result[1] != 150 {
		t.Errorf("expected median 150 at index 1, got %f", result[1])
	}
}

func TestMovingAverage_Smoothing(t *testing.T) {
	data := []float64{100, 102, 98, 103, 97}
	result := movingAverage(data, 3)
	mean := 100.0
	for _, v := range result {
		if math.Abs(v-mean) > 5 {
			t.Errorf("expected values close to %f, got %f", mean, v)
		}
	}
}

func TestMovingAverage_Constant(t *testing.T) {
	data := []float64{50, 50, 50, 50, 50}
	result := movingAverage(data, 5)
	for i, v := range result {
		if v != 50 {
			t.Errorf("index %d: expected 50, got %f", i, v)
		}
	}
}

func TestSmoothElevations_None(t *testing.T) {
	points := []gpx.TrackPoint{
		{Ele: 100},
		{Ele: 7583},
		{Ele: 100},
	}
	original := make([]float64, len(points))
	for i, p := range points {
		original[i] = p.Ele
	}

	SmoothElevations(points, SmoothNone)

	for i, p := range points {
		if p.Ele != original[i] {
			t.Errorf("SmoothNone modified point %d: expected %f, got %f", i, original[i], p.Ele)
		}
	}
}

func TestSmoothElevations_RemovesSpike(t *testing.T) {
	points := []gpx.TrackPoint{
		{Ele: 100}, {Ele: 100}, {Ele: 7583}, {Ele: 100}, {Ele: 100},
	}
	SmoothElevations(points, SmoothMedium)

	if points[2].Ele > 200 {
		t.Errorf("spike should be removed, got %f", points[2].Ele)
	}
}

func TestValidLevel(t *testing.T) {
	if !ValidLevel("none") {
		t.Error("'none' should be valid")
	}
	if !ValidLevel("medium") {
		t.Error("'medium' should be valid")
	}
	if ValidLevel("invalid") {
		t.Error("'invalid' should not be valid")
	}
}

// --- Gap-aware smoothing tests ---

func makeTestTime(secondsOffset int) time.Time {
	return time.Date(2024, 1, 1, 10, 0, 0, 0, time.UTC).Add(time.Duration(secondsOffset) * time.Second)
}

func TestGapIndices_NoGaps(t *testing.T) {
	times := []time.Time{
		makeTestTime(0), makeTestTime(5), makeTestTime(10), makeTestTime(15),
	}
	breaks := gapIndices(times, GapThreshold)
	if len(breaks) != 0 {
		t.Errorf("expected 0 breaks, got %d", len(breaks))
	}
}

func TestGapIndices_OneGap(t *testing.T) {
	times := []time.Time{
		makeTestTime(0), makeTestTime(5), makeTestTime(10),
		makeTestTime(910), makeTestTime(915), // 15-minute gap after index 2
	}
	breaks := gapIndices(times, GapThreshold)
	if len(breaks) != 1 || breaks[0] != 3 {
		t.Errorf("expected breaks=[3], got %v", breaks)
	}
}

func TestGapIndices_MultipleGaps(t *testing.T) {
	times := []time.Time{
		makeTestTime(0), makeTestTime(5),
		makeTestTime(905), makeTestTime(910), // 15-min gap at index 2
		makeTestTime(1810), makeTestTime(1815), // 15-min gap at index 4
	}
	breaks := gapIndices(times, GapThreshold)
	if len(breaks) != 2 || breaks[0] != 2 || breaks[1] != 4 {
		t.Errorf("expected breaks=[2,4], got %v", breaks)
	}
}

func TestMovingAverageSegmented_NoBleedAcrossGap(t *testing.T) {
	// Two groups: [10, 10, 10] then [100, 100, 100] with a gap between them
	data := []float64{10, 10, 10, 100, 100, 100}
	breaks := []int{3} // gap starts at index 3

	result := movingAverageSegmented(data, 5, breaks)

	// Within segment 1, values should stay at 10 (all identical)
	for i := 0; i < 3; i++ {
		if math.Abs(result[i]-10) > 0.01 {
			t.Errorf("segment 1 index %d: expected ~10, got %f", i, result[i])
		}
	}
	// Within segment 2, values should stay at 100 (all identical)
	for i := 3; i < 6; i++ {
		if math.Abs(result[i]-100) > 0.01 {
			t.Errorf("segment 2 index %d: expected ~100, got %f", i, result[i])
		}
	}
}

func TestMovingAverageSegmented_WithoutBreaks_WouldBleed(t *testing.T) {
	// Demonstrate that without segmentation, the gap DOES cause bleed
	data := []float64{10, 10, 10, 100, 100, 100}

	// Without segmentation: boundary points get averaged across groups
	resultUnseg := movingAverage(data, 5)
	// Point at index 2 (last of group 1) would be averaged with group 2 values
	if math.Abs(resultUnseg[2]-10) < 1 {
		t.Skip("no bleed detected in unsegmented case — test not meaningful")
	}

	// With segmentation: boundary points stay within their group
	resultSeg := movingAverageSegmented(data, 5, []int{3})
	if math.Abs(resultSeg[2]-10) > 0.01 {
		t.Errorf("segmented should prevent bleed: index 2 expected ~10, got %f", resultSeg[2])
	}
}

func TestMovingAverageSegmented_NoBreaks_SameAsOriginal(t *testing.T) {
	data := []float64{10, 20, 30, 40, 50}
	resultSeg := movingAverageSegmented(data, 3, nil)
	resultOrig := movingAverage(data, 3)

	for i := range data {
		if resultSeg[i] != resultOrig[i] {
			t.Errorf("index %d: segmented=%f, original=%f", i, resultSeg[i], resultOrig[i])
		}
	}
}

func TestMedianFilterSegmented_NoBleedAcrossGap(t *testing.T) {
	// Segment 1: [100, 100, 100], Segment 2: [7583, 100, 100]
	// Without segmentation, the spike at index 3 might bleed into segment 1
	data := []float64{100, 100, 100, 7583, 100, 100}
	breaks := []int{3}

	result := medianFilterSegmented(data, 5, breaks)

	// Segment 1 should be unchanged (all 100s)
	for i := 0; i < 3; i++ {
		if result[i] != 100 {
			t.Errorf("segment 1 index %d: expected 100, got %f", i, result[i])
		}
	}
	// Segment 2: spike at index 3 should be removed by median filter
	if result[3] > 200 {
		t.Errorf("spike at index 3 should be filtered, got %f", result[3])
	}
}

func TestSmoothElevations_GapAware(t *testing.T) {
	// Two segments with different elevations, separated by a 15-minute gap
	points := []gpx.TrackPoint{
		{Ele: 100, Time: makeTestTime(0)},
		{Ele: 100, Time: makeTestTime(5)},
		{Ele: 100, Time: makeTestTime(10)},
		{Ele: 500, Time: makeTestTime(910)}, // 15-minute gap, different elevation
		{Ele: 500, Time: makeTestTime(915)},
		{Ele: 500, Time: makeTestTime(920)},
	}

	SmoothElevations(points, SmoothMedium)

	// Segment 1 elevations should stay near 100 (no bleed from 500)
	for i := 0; i < 3; i++ {
		if math.Abs(points[i].Ele-100) > 10 {
			t.Errorf("segment 1 point %d: expected ~100, got %f (bleed from segment 2)", i, points[i].Ele)
		}
	}
	// Segment 2 elevations should stay near 500 (no bleed from 100)
	for i := 3; i < 6; i++ {
		if math.Abs(points[i].Ele-500) > 10 {
			t.Errorf("segment 2 point %d: expected ~500, got %f (bleed from segment 1)", i, points[i].Ele)
		}
	}
}
