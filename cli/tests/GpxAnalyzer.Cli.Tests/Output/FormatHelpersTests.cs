using GpxAnalyzer.Cli.Core.Output;

namespace GpxAnalyzer.Cli.Tests.Output;

public class FormatHelpersTests
{
    [Theory]
    [InlineData(0, "0s")]
    [InlineData(30, "30s")]
    [InlineData(90, "1m 30s")]
    [InlineData(3600, "1h 0m 0s")]
    [InlineData(3661, "1h 1m 1s")]
    [InlineData(86400, "1d 0h 0m 0s")]
    [InlineData(90061, "1d 1h 1m 1s")]
    public void FormatDuration_Seconds(double seconds, string expected)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        Assert.Equal(expected, FormatHelpers.FormatDuration(ts));
    }

    [Fact]
    public void FormatDistance_Meters()
    {
        Assert.Equal("1.0 km", FormatHelpers.FormatDistance(1000));
        Assert.Equal("500 m", FormatHelpers.FormatDistance(500));
        Assert.Equal("42.2 km", FormatHelpers.FormatDistance(42195));
    }

    [Fact]
    public void FormatSpeed_MetersPerSecond()
    {
        Assert.Equal("3.6 km/h", FormatHelpers.FormatSpeed(1.0));
        Assert.Equal("36.0 km/h", FormatHelpers.FormatSpeed(10.0));
    }

    [Fact]
    public void FormatElevation_Meters()
    {
        Assert.Equal("100 m", FormatHelpers.FormatElevation(100));
        Assert.Equal("0 m", FormatHelpers.FormatElevation(0));
        Assert.Equal("1234 m", FormatHelpers.FormatElevation(1234));
    }

    [Fact]
    public void FormatPace_ValidPace()
    {
        // Pace at 6 min/km = TimeSpan of 6 minutes
        var pace = TimeSpan.FromMinutes(6);
        Assert.Equal("6:00 min/km", FormatHelpers.FormatPace(pace));
    }

    [Fact]
    public void FormatPace_ZeroPace()
    {
        var pace = TimeSpan.Zero;
        Assert.Equal("-", FormatHelpers.FormatPace(pace));
    }
}
