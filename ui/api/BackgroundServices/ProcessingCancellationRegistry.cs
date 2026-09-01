namespace GpxAnalyzer.Api.BackgroundServices;

using System.Collections.Concurrent;

/// <summary>
/// Tracks the <see cref="CancellationTokenSource"/> of every activity the worker is
/// currently processing, so deleting an activity can stop its run rather than let it
/// finish an analysis — and a paid AI call — whose result has nowhere to go.
///
/// Cancellation is cooperative and best-effort: it is a cost and latency optimisation,
/// never a correctness guarantee. Deleting is safe whether or not the run notices, so
/// callers must not wait on it.
/// </summary>
public sealed class ProcessingCancellationRegistry
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _inFlight = new();

    public void Register(Guid activityId, CancellationTokenSource cts) => _inFlight[activityId] = cts;

    public void Unregister(Guid activityId) => _inFlight.TryRemove(activityId, out _);

    /// <returns><c>true</c> when a run was in flight and has been signalled.</returns>
    public bool Cancel(Guid activityId)
    {
        if (!_inFlight.TryRemove(activityId, out var cts)) return false;

        try
        {
            cts.Cancel();
            return true;
        }
        catch (ObjectDisposedException)
        {
            // The run finished between the lookup and the cancel — nothing to stop.
            return false;
        }
    }
}
