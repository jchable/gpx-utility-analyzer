namespace GpxAiAnalyzer.Core.Analysis;

using GpxAiAnalyzer.Core.Models;
using Microsoft.Extensions.AI;
using System.Text.Json;

/// <summary>
/// Orchestrates AI-powered track analysis using Microsoft.Extensions.AI.
/// </summary>
public sealed class TrackAnalyzer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
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

    public async Task<TrackReport> AnalyzeAsync(GpxStats stats, CancellationToken ct = default)
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
            ResponseFormat = ChatResponseFormat.Json,
        };

        var client = new ChatClientBuilder(_chatClient)
            .UseFunctionInvocation()
            .Build();

        var prompt = PromptBuilder.BuildAnalysisPrompt(stats);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, SystemPrompt),
            new(ChatRole.User, prompt),
        };

        var response = await client.GetResponseAsync(messages, chatOptions, ct);

        var text = response.Text
            ?? throw new InvalidOperationException("AI returned an empty response.");

        var report = JsonSerializer.Deserialize<TrackReport>(text, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize AI response into TrackReport.");

        return report;
    }
}
