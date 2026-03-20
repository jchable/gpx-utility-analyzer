namespace GpxAnalyzer.Api.Tests.Helpers;

/// <summary>
/// Forces all integration test classes to run sequentially (not in parallel).
/// This prevents background ActivityProcessingService workers from one ApiFactory
/// interfering with another factory's test data.
/// </summary>
[CollectionDefinition("Integration")]
public class IntegrationCollection;
