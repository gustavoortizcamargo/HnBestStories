using HnBestStories.Application.Dtos;
using HnBestStories.Application.Exceptions;
using HnBestStories.Application.Interfaces;
using HnBestStories.Domain.Entities;
using HnBestStories.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace HnBestStories.Application.Services;

public sealed class BestStoriesService : IBestStoriesService, ICacheWarmupService
{
    private const int MaxStories = 500;
    private const int MaxConcurrentItemRequests = 10;
    private static readonly SemaphoreSlim CacheRefreshLock = new(1, 1);

    private readonly IHackerNewsGateway _hackerNewsGateway;
    private readonly IStoryCache _storyCache;
    private readonly ILogger<BestStoriesService> _logger;

    public BestStoriesService(
        IHackerNewsGateway hackerNewsGateway,
        IStoryCache storyCache,
        ILogger<BestStoriesService> logger)
    {
        _hackerNewsGateway = hackerNewsGateway;
        _storyCache = storyCache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<StoryResponseDto>> GetBestStoriesAsync(int count, CancellationToken cancellationToken)
    {
        if (count <= 0 || count > MaxStories)
            throw new ArgumentOutOfRangeException(nameof(count), $"Count must be between 1 and {MaxStories}.");

        CachedStories? cachedStories = null;
        try
        {
            cachedStories = await _storyCache.GetTopStoriesAsync(cancellationToken);
            if (cachedStories is { IsStale: false })
            {
                _logger.LogInformation("CacheHit for best stories. RequestedCount={Count}", count);
                return cachedStories.Stories.Take(count).ToArray();
            }

            _logger.LogInformation("CacheMiss for best stories. RequestedCount={Count}", count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read best stories from cache. Falling back to Hacker News.");
        }

        try
        {
            var refreshed = await RefreshTopStoriesCoreAsync(cancellationToken);
            return refreshed.Take(count).ToArray();
        }
        catch (Exception ex) when (cachedStories is { Stories.Count: > 0 })
        {
            _logger.LogWarning(ex, "Returning stale cache because Hacker News refresh failed.");
            return cachedStories.Stories.Take(count).ToArray();
        }
        catch (Exception ex)
        {
            throw new HackerNewsUnavailableException("Unable to retrieve Hacker News stories and no cached data is available.", ex);
        }
    }

    public async Task WarmupAsync(CancellationToken cancellationToken)
    {
        await RefreshTopStoriesCoreAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<StoryResponseDto>> RefreshTopStoriesCoreAsync(CancellationToken cancellationToken)
    {
        await CacheRefreshLock.WaitAsync(cancellationToken);
        try
        {
            try
            {
                var cachedStories = await _storyCache.GetTopStoriesAsync(cancellationToken);
                if (cachedStories is { IsStale: false })
                {
                    _logger.LogInformation("CacheHit after refresh wait for best stories.");
                    return cachedStories.Stories;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to re-check best stories cache before refresh.");
            }

            var ids = await _hackerNewsGateway.GetBestStoryIdsAsync(cancellationToken);
            var selectedIds = ids.Take(MaxStories).ToArray();

            using var semaphore = new SemaphoreSlim(MaxConcurrentItemRequests);
            var tasks = selectedIds.Select(async id =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var item = await _hackerNewsGateway.GetItemAsync(id, cancellationToken);
                    return TryMap(item);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to retrieve Hacker News story. StoryId={StoryId}", id);
                    return null;
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var results = await Task.WhenAll(tasks);

            var stories = results
                .Where(story => story is not null)
                .Select(story => story!)
                .OrderByDescending(story => story.Score)
                .Take(MaxStories)
                .ToArray();

            try
            {
                await _storyCache.SetTopStoriesAsync(stories, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write best stories to cache.");
            }

            _logger.LogInformation("Best stories refreshed. StoryCount={StoryCount}", stories.Length);
            return stories;
        }
        finally
        {
            CacheRefreshLock.Release();
        }
    }

    private static StoryResponseDto? TryMap(HackerNewsItemDto? item)
    {
        if (item is null || item.Deleted == true || item.Dead == true)
            return null;

        if (!string.Equals(item.Type, "story", StringComparison.OrdinalIgnoreCase))
            return null;

        if (string.IsNullOrWhiteSpace(item.Title) || item.Score < 0 || item.Descendants < 0)
            return null;

        var uri = string.IsNullOrWhiteSpace(item.Url)
            ? $"https://news.ycombinator.com/item?id={item.Id}"
            : item.Url;

        try
        {
            var story = Story.Create(
                item.Id,
                item.Title,
                uri,
                item.By ?? string.Empty,
                DateTimeOffset.FromUnixTimeSeconds(item.Time),
                item.Score,
                item.Descendants);

            return new StoryResponseDto(
                story.Title,
                story.Uri,
                story.PostedBy,
                story.Time,
                story.Score,
                story.CommentCount);
        }
        catch (DomainException)
        {
            return null;
        }
    }
}
