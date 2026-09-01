namespace GpxAiAnalyzer.Core.Analysis;

using GpxAiAnalyzer.Core.Models;
using Microsoft.Extensions.AI;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Orchestrates AI-powered track analysis using Microsoft.Extensions.AI.
/// </summary>
public sealed class TrackAnalyzer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private const string SystemPrompt = """
        You are an expert outdoor activity analyst. You analyze GPS track statistics
        to assess difficulty, identify key segments, and provide recommendations.
        Use the available tools to compute derived metrics before forming your assessment.
        Always call the EstimateDifficulty and ClassifyActivity tools with the provided data.
        When biometric data (heart rate, power, cadence, temperature) is available, use the
        EstimateTrainingStress and ClassifyIntensity tools for deeper physiological analysis.
        Base your analysis on quantitative metrics, not assumptions.
        Respond with a JSON object matching the required schema.
        """;

    private readonly IChatClient _chatClient;

    public TrackAnalyzer(IChatClient chatClient)
    {
        _chatClient = chatClient;
    }

    /// <summary>
    /// Extracts the JSON object from a model response. Never trusts the response to
    /// contain nothing but JSON: a response that STARTS with '{' can still append a
    /// closing remark, and System.Text.Json rejects trailing content after the
    /// top-level value.
    /// </summary>
    internal static string ExtractJson(string text)
    {
        var trimmed = text.Trim();

        var startIdx = trimmed.IndexOf('{');
        var endIdx = trimmed.LastIndexOf('}');
        if (startIdx >= 0 && endIdx > startIdx)
            return trimmed[startIdx..(endIdx + 1)];

        return trimmed;
    }

    public async Task<TrackReport> AnalyzeAsync(GpxStats stats, string language = "en", CancellationToken ct = default)
    {
        var tools = new AITool[]
        {
            AIFunctionFactory.Create(AnalysisTools.GetSteepnessRatio),
            AIFunctionFactory.Create(AnalysisTools.ClassifyActivity),
            AIFunctionFactory.Create(AnalysisTools.EstimateDifficulty),
            AIFunctionFactory.Create(AnalysisTools.GetStopFrequency),
            AIFunctionFactory.Create(AnalysisTools.EstimateTrainingStress),
            AIFunctionFactory.Create(AnalysisTools.ClassifyIntensity),
        };

        var chatOptions = new ChatOptions
        {
            Tools = [.. tools],
        };

        var client = new ChatClientBuilder(_chatClient)
            .UseFunctionInvocation()
            .Build();

        var prompt = PromptBuilder.BuildAnalysisPrompt(stats, language);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, SystemPrompt),
            new(ChatRole.User, prompt),
        };

        var response = await client.GetResponseAsync(messages, chatOptions, ct);

        // ChatResponse.Text is non-nullable and returns "" when the response
        // carried no text — a model that ends on tool calls only, or that hits the
        // function-invocation iteration limit. The old "?? throw" was dead code and
        // the user got an opaque JsonException instead.
        var text = response.Text;
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException(
                "AI returned an empty response (no assistant text — the model may have " +
                "ended on tool calls or hit the function-invocation limit).");

        // Strip markdown code fences if the model wraps JSON in ```json ... ```
        var json = ExtractJson(text);

        var report = JsonSerializer.Deserialize<TrackReport>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize AI response into TrackReport.");

        return report;
    }
}
