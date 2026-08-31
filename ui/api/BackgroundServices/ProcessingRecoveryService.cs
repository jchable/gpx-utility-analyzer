namespace GpxAnalyzer.Api.BackgroundServices;

using System.Threading.Channels;
using GpxAnalyzer.Api.Data;
using GpxAnalyzer.Api.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Re-enqueues activities left in a non-terminal state by a previous process.
/// The processing queue is an in-memory Channel, so a restart loses every queued
/// id and abandons whatever was in flight; without this, those rows never reach
/// Completed or Failed and the client polls a status that will never change.
/// </summary>
public class ProcessingRecoveryService : IHostedService
{
    private static readonly ProcessingStatus[] NonTerminal =
    [
        ProcessingStatus.Pending,
        ProcessingStatus.Analyzing,
        ProcessingStatus.AiProcessing,
    ];

    private readonly Channel<(Guid ActivityId, Guid UserId)> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProcessingRecoveryService> _logger;

    public ProcessingRecoveryService(
        Channel<(Guid ActivityId, Guid UserId)> channel,
        IServiceScopeFactory scopeFactory,
        ILogger<ProcessingRecoveryService> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var stranded = await db.Activities
            .Where(a => NonTerminal.Contains(a.Status))
            .Select(a => new { a.Id, a.UserId })
            .ToListAsync(cancellationToken);

        if (stranded.Count == 0) return;

        _logger.LogWarning(
            "Re-enqueueing {Count} activities left in a non-terminal state by a previous run",
            stranded.Count);

        foreach (var a in stranded)
        {
            // Reset to Pending so the status the client sees matches reality.
            await db.Activities.Where(x => x.Id == a.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ProcessingStatus.Pending),
                    cancellationToken);
            await _channel.Writer.WriteAsync((a.Id, a.UserId), cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
