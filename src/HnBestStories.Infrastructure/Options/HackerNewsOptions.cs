namespace HnBestStories.Infrastructure.Options;

public sealed class HackerNewsOptions
{
    public const string SectionName = "HackerNews";
    public string BaseUrl { get; init; } = "https://hacker-news.firebaseio.com/v0/";
}
