namespace GpxAnalyzer.Api.BackgroundServices;

public readonly record struct ProcessingRequest(Guid ActivityId, Guid UserId, Guid LeaseId);