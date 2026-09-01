namespace GpxAnalyzer.Api.BackgroundServices;

using System.Threading.Channels;
using GpxAnalyzer.Api.Data;
using GpxAnalyzer.Api.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Reclaims activities nothing is working on any more.
///
/// The processing queue is an in-memory Channel, so a restart loses every queued id
/// and abandons whatever was in flight; without this, those rows never reach
/// Completed or Failed and the client polls a status that will never change.
///
/// Reclaiming only at startup was not enough: a crash inside the one-minute lease
/// window left a row whose lease was still live, which the startup pass had already
/// skipped, so the activity stayed stuck until the next restart. The sweeper
/// therefore keeps running and reclaims leases as they expire.
///
/// This assumes a SINGLE API replica — see docker-compose.prod.yml and the
/// deployment docs. With more than one, every replica would reclaim and re-enqueue
/// the same rows.
/// </summary>
public class ProcessingRecoveryService : IHostedService, IDisposable
{
    private static readonly ProcessingStatus[] NonTerminal =
    [
        ProcessingStatus.Pending,
        ProcessingStatus.Recovering,
        ProcessingStatus.Analyzing,
        ProcessingStatus.AiProcessing,
    ];

    private static readonly TimeSpan DefaultSweepInterval = TimeSpan.FromSeconds(30);

    /// <summary>How long a reclaimed row is held before another sweep may take it.</summary>
    private static readonly TimeSpan ReclaimLease = TimeSpan.FromMinutes(1);

    private readonly Channel<ProcessingRequest> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProcessingRecoveryService> _logger;
    private readonly TimeSpan _sweepInterval;

    private CancellationTokenSource? _stopping;
    private Task? _sweeper;

    public ProcessingRecoveryService(
        Channel<ProcessingRequest> channel,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<ProcessingRecoveryService> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _logger = logger;

        var seconds = configuration.GetValue<int?>("Processing:LeaseSweepIntervalSeconds");
        _sweepInterval = seconds is > 0
            ? TimeSpan.FromSeconds(seconds.Value)
            : DefaultSweepInterval;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // At startup this process holds no leases, so EVERY non-terminal row is
        // stranded — whatever its lease still claims.
        await SweepAsync(reclaimLiveLeases: true, cancellationToken);

        _stopping = new CancellationTokenSource();
        _sweeper = SweepLoopAsync(_stopping.Token);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_stopping is null) return;

        await _stopping.CancelAsync();
        if (_sweeper is not null)
        {
            try { await _sweeper.WaitAsync(cancellationToken); }
            catch (OperationCanceledException) { /* shutting down anyway */ }
        }
    }

    private async Task SweepLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_sweepInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                try
                {
                    await SweepAsync(reclaimLiveLeases: false, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    // One bad sweep must not stop every later one.
                    _logger.LogError(ex, "Lease sweep failed; will retry in {Interval}", _sweepInterval);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    /// <param name="reclaimLiveLeases">
    /// Startup only. During normal operation an unexpired lease means a worker in
    /// this process genuinely owns the row and it must be left alone.
    /// </param>
    private async Task SweepAsync(bool reclaimLiveLeases, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;
        var stranded = await db.Activities
            .Where(a => NonTerminal.Contains(a.Status) &&
                (reclaimLiveLeases ||
                 a.ProcessingLeaseExpiresAt == null ||
                 a.ProcessingLeaseExpiresAt <= now))
            .Select(a => new { a.Id, a.UserId, a.Status, a.ProcessingLeaseId })
            .ToListAsync(ct);

        if (stranded.Count == 0) return;

        _logger.LogWarning(
            reclaimLiveLeases
                ? "Re-enqueueing {Count} activities left in a non-terminal state by a previous run"
                : "Reclaiming {Count} activities whose processing lease expired",
            stranded.Count);

        foreach (var a in stranded)
        {
            var leaseId = Guid.NewGuid();

            // Conditional on the status and lease we read, so a worker that claims
            // the row between the query and here keeps it.
            var claimed = await db.Activities
                .Where(x => x.Id == a.Id && x.Status == a.Status &&
                    x.ProcessingLeaseId == a.ProcessingLeaseId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Status, ProcessingStatus.Recovering)
                    .SetProperty(x => x.ProcessingLeaseId, leaseId)
                    .SetProperty(x => x.ProcessingLeaseExpiresAt, now.Add(ReclaimLease)),
                    ct);

            if (claimed == 1)
                await _channel.Writer.WriteAsync(
                    new ProcessingRequest(a.Id, a.UserId, leaseId), ct);
        }
    }

    public void Dispose() => _stopping?.Dispose();
}
