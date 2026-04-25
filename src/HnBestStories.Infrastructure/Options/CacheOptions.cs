namespace HnBestStories.Infrastructure.Options;

public sealed class CacheOptions
{
    public const string SectionName = "Cache";
    public int BestStoriesTopTtlMinutes { get; init; } = 5;
    public int StaleCacheTtlMinutes { get; init; } = 60;
}
