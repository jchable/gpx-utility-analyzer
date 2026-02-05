namespace GpxAiAnalyzer.Tests.Analysis;

using GpxAiAnalyzer.Analysis;

public class AnalysisToolsTests
{
    [Fact]
    public void GetSteepnessRatio_FlatTrack_ReturnsZero()
    {
        var ratio = AnalysisTools.GetSteepnessRatio(0, 10);
        Assert.Equal(0, ratio);
    }

    [Fact]
    public void GetSteepnessRatio_TenPercentGrade()
    {
        // 100m gain over 1km = 10%
        var ratio = AnalysisTools.GetSteepnessRatio(100, 1);
        Assert.Equal(10, ratio, precision: 1);
    }

    [Fact]
    public void GetSteepnessRatio_ZeroDistance_ReturnsZero()
    {
        var ratio = AnalysisTools.GetSteepnessRatio(500, 0);
        Assert.Equal(0, ratio);
    }

    [Fact]
    public void ClassifyActivity_HighSpeed_ReturnsCycling()
    {
        var result = AnalysisTools.ClassifyActivity(20, 200, 50);
        Assert.Equal("cycling", result);
    }

    [Fact]
    public void ClassifyActivity_MediumSpeed_ReturnsTrailRunning()
    {
        var result = AnalysisTools.ClassifyActivity(10, 500, 20);
        Assert.Equal("trail-running", result);
    }

    [Fact]
    public void ClassifyActivity_SteepTerrain_ReturnsMountaineering()
    {
        // 1500m gain over 10km = 150m/km ratio > 100
        var result = AnalysisTools.ClassifyActivity(3, 1500, 10);
        Assert.Equal("mountaineering", result);
    }

    [Fact]
    public void ClassifyActivity_SlowFlat_ReturnsHiking()
    {
        var result = AnalysisTools.ClassifyActivity(4, 200, 15);
        Assert.Equal("hiking", result);
    }

    [Fact]
    public void EstimateDifficulty_ShortEasyTrack_ReturnsLow()
    {
        // 5km, 100m gain → effort = 5 + 1 = 6 → 6/5 = 1
        var score = AnalysisTools.EstimateDifficulty(5, 100, 2);
        Assert.InRange(score, 1, 3);
    }

    [Fact]
    public void EstimateDifficulty_LongHardTrack_ReturnsHigh()
    {
        // 40km, 3000m gain → effort = 40 + 30 = 70 → 70/5 = 14 → clamped to 10
        var score = AnalysisTools.EstimateDifficulty(40, 3000, 12);
        Assert.Equal(10, score);
    }

    [Fact]
    public void EstimateDifficulty_ClampedToMinimum()
    {
        var score = AnalysisTools.EstimateDifficulty(1, 0, 0.5);
        Assert.Equal(1, score);
    }

    [Fact]
    public void GetStopFrequency_NoStops_ReturnsZero()
    {
        var freq = AnalysisTools.GetStopFrequency(0, 10);
        Assert.Equal(0, freq);
    }

    [Fact]
    public void GetStopFrequency_ZeroDistance_ReturnsZero()
    {
        var freq = AnalysisTools.GetStopFrequency(5, 0);
        Assert.Equal(0, freq);
    }

    [Fact]
    public void GetStopFrequency_CalculatesCorrectly()
    {
        // 5 stops over 10km = 0.5 stops/km
        var freq = AnalysisTools.GetStopFrequency(5, 10);
        Assert.Equal(0.5, freq, precision: 2);
    }
}
