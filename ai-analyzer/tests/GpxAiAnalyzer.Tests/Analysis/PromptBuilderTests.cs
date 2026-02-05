namespace GpxAiAnalyzer.Tests.Analysis;

using GpxAiAnalyzer.Analysis;
using GpxAiAnalyzer.Models;

public class PromptBuilderTests
{
    [Fact]
    public void BuildAnalysisPrompt_ContainsFilename()
    {
        var stats = CreateSampleStats();
        var prompt = PromptBuilder.BuildAnalysisPrompt(stats);
        Assert.Contains("test-track.gpx", prompt);
    }

    [Fact]
    public void BuildAnalysisPrompt_ContainsDistance()
    {
        var stats = CreateSampleStats();
        var prompt = PromptBuilder.BuildAnalysisPrompt(stats);
        Assert.Contains("15.2 km", prompt);
    }

    [Fact]
    public void BuildAnalysisPrompt_ContainsElevation()
    {
        var stats = CreateSampleStats();
        var prompt = PromptBuilder.BuildAnalysisPrompt(stats);
        Assert.Contains("+800m", prompt);
        Assert.Contains("-750m", prompt);
    }

    [Fact]
    public void BuildAnalysisPrompt_ContainsTimeMetrics()
    {
        var stats = CreateSampleStats();
        var prompt = PromptBuilder.BuildAnalysisPrompt(stats);
        Assert.Contains("6h 30m", prompt);
        Assert.Contains("5h 45m", prompt);
    }

    [Fact]
    public void BuildAnalysisPrompt_ContainsSpeedMetrics()
    {
        var stats = CreateSampleStats();
        var prompt = PromptBuilder.BuildAnalysisPrompt(stats);
        Assert.Contains("2.3 km/h avg", prompt);
    }

    [Fact]
    public void BuildAnalysisPrompt_ContainsRequiredAnalysisSections()
    {
        var stats = CreateSampleStats();
        var prompt = PromptBuilder.BuildAnalysisPrompt(stats);
        Assert.Contains("difficulty", prompt);
        Assert.Contains("key_segments", prompt);
        Assert.Contains("recommendations", prompt);
        Assert.Contains("summary", prompt);
        Assert.Contains("effort", prompt);
    }

    [Fact]
    public void BuildAnalysisPrompt_ContainsStopInfo()
    {
        var stats = CreateSampleStats();
        var prompt = PromptBuilder.BuildAnalysisPrompt(stats);
        Assert.Contains("3 stops", prompt);
    }

    [Fact]
    public void BuildAnalysisPrompt_ContainsLongestStop()
    {
        var stats = CreateSampleStats();
        var prompt = PromptBuilder.BuildAnalysisPrompt(stats);
        Assert.Contains("Longest stop", prompt);
        Assert.Contains("30m", prompt);
    }

    [Fact]
    public void BuildAnalysisPrompt_MentionsToolNames()
    {
        var stats = CreateSampleStats();
        var prompt = PromptBuilder.BuildAnalysisPrompt(stats);
        Assert.Contains("EstimateDifficulty", prompt);
        Assert.Contains("ClassifyActivity", prompt);
    }

    private static GpxStats CreateSampleStats() => new()
    {
        Filename = "test-track.gpx",
        TotalDistanceM = 15234.5,
        TotalDistance3dM = 15450.2,
        TotalDistanceKm = 15.2345,
        ElevationGainM = 800,
        ElevationLossM = 750,
        MaxElevationM = 1250,
        MinElevationM = 450,
        TotalTime = new DurationValue { Display = "6h 30m", Seconds = 23400 },
        MovingTime = new DurationValue { Display = "5h 45m", Seconds = 20700 },
        StoppedTime = new DurationValue { Display = "45m", Seconds = 2700 },
        AvgSpeedKmh = 2.34,
        AvgMovingSpeedKmh = 2.65,
        MaxSpeedKmh = 8.5,
        AvgPace = "25:38 min/km",
        AvgMovingPace = "22:38 min/km",
        PointCount = 2500,
        SegmentCount = 3,
        PointsPerKm = 164.3,
        StopCount = 3,
        TotalStopTime = new DurationValue { Display = "45m", Seconds = 2700 },
        AvgStopDuration = new DurationValue { Display = "15m", Seconds = 900 },
        LongestStop = new StopInfo
        {
            StartTime = "2024-01-01T11:30:00Z",
            EndTime = "2024-01-01T12:00:00Z",
            Duration = new DurationValue { Display = "30m", Seconds = 1800 },
            Lat = 48.8580,
            Lon = 2.3550
        },
        Stops =
        [
            new StopInfo
            {
                StartTime = "2024-01-01T10:00:00Z",
                EndTime = "2024-01-01T10:10:00Z",
                Duration = new DurationValue { Display = "10m", Seconds = 600 },
                Lat = 48.8500,
                Lon = 2.3500
            },
            new StopInfo
            {
                StartTime = "2024-01-01T11:30:00Z",
                EndTime = "2024-01-01T12:00:00Z",
                Duration = new DurationValue { Display = "30m", Seconds = 1800 },
                Lat = 48.8580,
                Lon = 2.3550
            },
            new StopInfo
            {
                StartTime = "2024-01-01T13:00:00Z",
                EndTime = "2024-01-01T13:05:00Z",
                Duration = new DurationValue { Display = "5m", Seconds = 300 },
                Lat = 48.8600,
                Lon = 2.3600
            }
        ]
    };
}
