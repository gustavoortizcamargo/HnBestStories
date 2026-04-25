namespace HnBestStories.Worker;

public sealed class WorkerOptions
{
    public const string SectionName = "Worker";
    public int RefreshIntervalMinutes { get; init; } = 5;
}
