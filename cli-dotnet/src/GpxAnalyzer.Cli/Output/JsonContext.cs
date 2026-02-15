using System.Text.Json.Serialization;

namespace GpxAnalyzer.Cli.Output;

[JsonSerializable(typeof(JsonSummary))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class JsonContext : JsonSerializerContext;
