using GpxAnalyzer.Cli.Core.Stats;

namespace GpxAnalyzer.Cli.Core.Output;

public interface IFormatter
{
    void Format(TextWriter writer, string filename, Summary summary, StopConfig config);
}

public static class FormatterFactory
{
    public static IFormatter Create(string format) => format switch
    {
        "text" => new TextFormatter(),
        "json" => new JsonFormatter(),
        _ => throw new ArgumentException($"Unknown format '{format}', expected 'text' or 'json'")
    };
}
