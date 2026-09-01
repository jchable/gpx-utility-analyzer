using System.Globalization;
using GpxAnalyzer.Cli.Core.Anomaly;
using GpxAnalyzer.Cli.Core.Output;
using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Tests.Output;

public class SummaryMapperTests
{
    [Fact]
    public void ToGpxStats_ValidSummary_MapsAllFieldsCorrectly()
    {
        var summary = new Summary
        {
            TotalDistance = 12345.6,
            TotalDistance3D = 12400.0,
            Elevation = new ElevationResult { Gain = 500.0, Loss = 480.0, Max = 1200.0, Min = 700.0 },
            TotalTime = TimeSpan.FromHours(3),
            MovingTime = TimeSpan.FromHours(2.5),
            StoppedTime = TimeSpan.FromMinutes(30),
            StartTime = new DateTime(2024, 6, 15, 8, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2024, 6, 15, 11, 0, 0, DateTimeKind.Utc),
            Speed = new SpeedResult
            {
                AvgSpeed = 1.143,
                AvgMovingSpeed = 1.372,
                MaxSpeed = 3.5,
                AvgPace = TimeSpan.FromSeconds(875),
                AvgMovingPace = TimeSpan.FromSeconds(729),
            },
            PointCount = 3000,
            SegmentCount = 1,
            PointsPerKm = 243.0,
            StopCount = 2,
            TotalStopTime = TimeSpan.FromMinutes(30),
            AvgStopDuration = TimeSpan.FromMinutes(15),
            LongestStop = new Stop
            {
                StartTime = new DateTime(2024, 6, 15, 9, 30, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2024, 6, 15, 9, 50, 0, DateTimeKind.Utc),
                Duration = TimeSpan.FromMinutes(20),
                Lat = 45.0,
                Lon = 6.0,
            },
            Stops =
            [
                new Stop
                {
                    StartTime = new DateTime(2024, 6, 15, 9, 30, 0, DateTimeKind.Utc),
                    EndTime = new DateTime(2024, 6, 15, 9, 50, 0, DateTimeKind.Utc),
                    Duration = TimeSpan.FromMinutes(20),
                    Lat = 45.0,
                    Lon = 6.0,
                },
                new Stop
                {
                    StartTime = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc),
                    EndTime = new DateTime(2024, 6, 15, 10, 40, 0, DateTimeKind.Utc),
                    Duration = TimeSpan.FromMinutes(10),
                    Lat = 45.1,
                    Lon = 6.1,
                },
            ],
        };

        var stats = SummaryMapper.ToGpxStats("test.gpx", summary);

        // Distance
        Assert.Equal(12345.6, stats.TotalDistanceM);
        Assert.Equal(12400.0, stats.TotalDistance3dM);
        Assert.Equal(12345.6 / 1000, stats.TotalDistanceKm);

        // Elevation
        Assert.Equal(500.0, stats.ElevationGainM);
        Assert.Equal(480.0, stats.ElevationLossM);
        Assert.Equal(1200.0, stats.MaxElevationM);
        Assert.Equal(700.0, stats.MinElevationM);

        // Time
        Assert.Equal("2024-06-15T08:00:00Z", stats.StartTime);
        Assert.Equal("2024-06-15T11:00:00Z", stats.EndTime);
        Assert.Equal(10800, stats.TotalTime.Seconds);
        Assert.Equal(9000, stats.MovingTime.Seconds);

        // Speed (m/s → km/h)
        Assert.Equal(1.143 * 3.6, stats.AvgSpeedKmh, 3);
        Assert.Equal(1.372 * 3.6, stats.AvgMovingSpeedKmh, 3);
        Assert.Equal(3.5 * 3.6, stats.MaxSpeedKmh, 3);

        // Points
        Assert.Equal(3000, stats.PointCount);
        Assert.Equal(1, stats.SegmentCount);
        Assert.Equal("test.gpx", stats.Filename);

        // Stops
        Assert.Equal(2, stats.StopCount);
        Assert.NotNull(stats.LongestStop);
        Assert.Equal(45.0, stats.LongestStop.Lat);
        Assert.NotNull(stats.Stops);
        Assert.Equal(2, stats.Stops.Count);
    }

    [Fact]
    public void ToGpxStats_NoBiometrics_ReturnsNullBiometricFields()
    {
        var summary = new Summary
        {
            TotalDistance = 1000,
            Elevation = new ElevationResult(),
            Speed = new SpeedResult(),
            Biometrics = new BiometricsResult(),
        };

        var stats = SummaryMapper.ToGpxStats("test.gpx", summary);

        Assert.Null(stats.HeartRate);
        Assert.Null(stats.Power);
        Assert.Null(stats.Cadence);
        Assert.Null(stats.Temperature);
        Assert.Null(stats.Stops);
        Assert.Null(stats.LongestStop);
    }

    // #87 — ':' is the time-separator PLACEHOLDER in a .NET custom date/time
    // format string, not a literal. SummaryMapper lives in the Core library but
    // is called from ui/api, which runs under the OS culture.
    [Theory]
    [InlineData("fi-FI")]
    [InlineData("da-DK")]
    public void ToGpxStats_UnderACultureWithANonColonTimeSeparator_EmitsIsoTimestamps(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);

            // Guard against a vacuous run: this culture must actually use a
            // non-colon time separator on this ICU build, otherwise the test
            // would pass without ever exercising the bug.
            Assert.NotEqual(":", CultureInfo.CurrentCulture.DateTimeFormat.TimeSeparator);

            var s = new Summary
            {
                StartTime = new DateTime(2024, 6, 15, 8, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2024, 6, 15, 11, 30, 0, DateTimeKind.Utc),
                Stops =
                [
                    new Stop
                    {
                        StartTime = new DateTime(2024, 6, 15, 9, 30, 0, DateTimeKind.Utc),
                        EndTime = new DateTime(2024, 6, 15, 9, 50, 0, DateTimeKind.Utc),
                        Duration = TimeSpan.FromMinutes(20),
                        Lat = 45.0,
                        Lon = 6.0,
                    },
                ],
                AnomalyReport = new AnomalyReport
                {
                    QualityScore = 95,
                    Anomalies =
                    [
                        new TrackAnomaly
                        {
                            Type = AnomalyType.SignalLoss,
                            Severity = AnomalySeverity.Warning,
                            Category = AnomalyCategory.Position,
                            StartIndex = 0,
                            EndIndex = 1,
                            StartTime = new DateTime(2024, 6, 15, 10, 0, 0, DateTimeKind.Utc),
                            EndTime = new DateTime(2024, 6, 15, 10, 5, 0, DateTimeKind.Utc),
                            Description = "Signal loss",
                        },
                    ],
                },
            };

            var stats = SummaryMapper.ToGpxStats("track.gpx", s);

            Assert.Equal("2024-06-15T08:00:00Z", stats.StartTime);
            Assert.Equal("2024-06-15T11:30:00Z", stats.EndTime);
            Assert.NotNull(stats.Stops);
            Assert.Equal("2024-06-15T09:30:00Z", stats.Stops[0].StartTime);
            Assert.Equal("2024-06-15T09:50:00Z", stats.Stops[0].EndTime);

            Assert.NotNull(stats.Anomalies);
            Assert.NotNull(stats.Anomalies.Anomalies);
            Assert.Equal("2024-06-15T10:00:00Z", stats.Anomalies.Anomalies[0].StartTime);
            Assert.Equal("2024-06-15T10:05:00Z", stats.Anomalies.Anomalies[0].EndTime);

            // And the API must be able to parse its own producer's output.
            Assert.True(DateTime.TryParse(stats.StartTime,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out _));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
