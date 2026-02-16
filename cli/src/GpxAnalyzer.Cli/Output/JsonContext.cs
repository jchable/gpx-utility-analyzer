using System.Text.Json.Serialization;
using GpxAnalyzer.Cli.Core.Output;

namespace GpxAnalyzer.Cli.Output;

[JsonSerializable(typeof(JsonSummary))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class JsonContext : JsonSerializerContext;
