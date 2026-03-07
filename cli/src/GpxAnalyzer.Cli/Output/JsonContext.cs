using System.Text.Json.Serialization;
using GpxAnalyzer.Cli.Core.Output;

namespace GpxAnalyzer.Cli.Output;

[JsonSerializable(typeof(JsonSummary))]
[JsonSerializable(typeof(JsonAnomalyReport))]
[JsonSerializable(typeof(JsonAnomaly))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class JsonContext : JsonSerializerContext;
