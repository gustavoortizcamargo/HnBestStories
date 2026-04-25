namespace HnBestStories.Infrastructure.Options;

public sealed class ResilienceOptions
{
    public const string SectionName = "Resilience";
    public int TimeoutSeconds { get; init; } = 5;
    public int RetryCount { get; init; } = 3;
    public int CircuitBreakerHandledEventsAllowedBeforeBreaking { get; init; } = 5;
    public int CircuitBreakerDurationSeconds { get; init; } = 30;
}
