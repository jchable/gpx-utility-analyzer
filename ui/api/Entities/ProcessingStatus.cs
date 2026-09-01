namespace GpxAnalyzer.Api.Entities;

public enum ProcessingStatus
{
    Pending,
    Recovering,
    Analyzing,
    AiProcessing,
    Completed,
    Failed
}
