namespace GpxAiAnalyzer.Tests.Analysis;

using GpxAiAnalyzer.Core.Analysis;

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

    [Fact]
    public void EstimateTrainingStress_ZeroThreshold_ReturnsZero()
    {
        var tss = AnalysisTools.EstimateTrainingStress(200, 0, 1);
        Assert.Equal(0, tss);
    }

    [Fact]
    public void EstimateTrainingStress_AtThreshold_Returns100PerHour()
    {
        // NP = FTP → IF = 1.0 → TSS = 1*1*1*100 = 100
        var tss = AnalysisTools.EstimateTrainingStress(250, 250, 1);
        Assert.Equal(100, tss, precision: 1);
    }

    [Fact]
    public void EstimateTrainingStress_AboveThreshold()
    {
        // NP=300, FTP=250 → IF=1.2 → TSS = 1.44 * 2 * 100 = 288
        var tss = AnalysisTools.EstimateTrainingStress(300, 250, 2);
        Assert.Equal(288, tss, precision: 1);
    }

    [Fact]
    public void ClassifyIntensity_HighPercent_ReturnsHighIntensity()
    {
        var result = AnalysisTools.ClassifyIntensity(60);
        Assert.Equal("high-intensity", result);
    }

    [Fact]
    public void ClassifyIntensity_ModeratePercent_ReturnsModerate()
    {
        var result = AnalysisTools.ClassifyIntensity(35);
        Assert.Equal("moderate-intensity", result);
    }

    [Fact]
    public void ClassifyIntensity_LowPercent_ReturnsLow()
    {
        var result = AnalysisTools.ClassifyIntensity(10);
        Assert.Equal("low-intensity", result);
    }

    [Fact]
    public void ClassifyIntensity_BoundaryAt50_ReturnsModerate()
    {
        var result = AnalysisTools.ClassifyIntensity(50);
        Assert.Equal("moderate-intensity", result);
    }

    [Fact]
    public void ClassifyIntensity_BoundaryAt20_ReturnsLow()
    {
        var result = AnalysisTools.ClassifyIntensity(20);
        Assert.Equal("low-intensity", result);
    }
}
