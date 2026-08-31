using System.Text.Json;
using System.Text.Json.Serialization;
using GpxAiAnalyzer.Core.Analysis;
using GpxAiAnalyzer.Core.Models;
using Microsoft.Extensions.AI;

namespace GpxAiAnalyzer.Tests.Analysis;

public class TrackAnalyzerTests
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    [Fact]
    public void ExtractJson_JsonFollowedByProse_ReturnsOnlyTheJson()
    {
        const string response =
            """{"difficulty":{"grade":"Easy","score":1,"justification":"j"},"summary":"s"}""" +
            "\n\nLet me know if you'd like a deeper breakdown of the climbs.";

        var json = TrackAnalyzer.ExtractJson(response);

        // Must be deserializable: System.Text.Json rejects trailing content.
        var report = JsonSerializer.Deserialize<TrackReport>(json, Opts);
        Assert.NotNull(report);
        Assert.Equal("Easy", report!.Difficulty.Grade);
    }

    [Fact]
    public void ExtractJson_ProsePreambleThenJson_StillWorks()
    {
        const string response =
            "Here is the analysis:\n```json\n{\"summary\":\"s\"}\n```";
        var report = JsonSerializer.Deserialize<TrackReport>(
            TrackAnalyzer.ExtractJson(response), Opts);
        Assert.NotNull(report);
        Assert.Equal("s", report!.Summary);
    }

    [Fact]
    public void ExtractJson_PlainJson_IsUnchanged()
    {
        const string response = """{"summary":"s"}""";
        Assert.Equal(response, TrackAnalyzer.ExtractJson(response));
    }

    // #112: ChatResponse.Text is non-nullable and yields "" when the model ended on
    // tool calls or hit the function-invocation limit, so the "?? throw" guard was
    // dead code and the caller got an opaque JsonException instead.
    [Fact]
    public async Task AnalyzeAsync_EmptyModelResponse_ThrowsDiagnosableError()
    {
        var analyzer = new TrackAnalyzer(new EmptyResponseChatClient());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => analyzer.AnalyzeAsync(new GpxStats()));

        Assert.Contains("empty response", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class EmptyResponseChatClient : IChatClient
    {
        public void Dispose() { }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "")));

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
    }
}
