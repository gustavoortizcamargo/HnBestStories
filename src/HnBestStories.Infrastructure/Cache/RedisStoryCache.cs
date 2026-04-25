using System.Text.Json;
using HnBestStories.Application.Dtos;
using HnBestStories.Application.Interfaces;
using HnBestStories.Infrastructure.Options;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HnBestStories.Infrastructure.Cache;

public sealed class RedisStoryCache : IStoryCache
{
    private const string TopStoriesKey = "hn:beststories:top500";
    private readonly IDistributedCache _cache;
    private readonly CacheOptions _options;
    private readonly ILogger<RedisStoryCache> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private CacheEnvelope? _localCache;

    public RedisStoryCache(IDistributedCache cache, IOptions<CacheOptions> options, ILogger<RedisStoryCache> logger)
    {
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<CachedStories?> GetTopStoriesAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var localCache = _localCache;
        if (IsUsable(localCache, now))
        {
            return ToCachedStories(localCache!, now);
        }

        string? json;
        try
        {
            json = await _cache.GetStringAsync(TopStoriesKey, cancellationToken);
        }
        catch (Exception ex) when (localCache is not null)
        {
            _logger.LogWarning(ex, "Failed to read top stories from Redis. Falling back to local cache.");
            return ToCachedStories(localCache, now);
        }

        if (string.IsNullOrWhiteSpace(json))
            return null;

        var envelope = JsonSerializer.Deserialize<CacheEnvelope>(json, _jsonOptions);
        if (envelope?.Stories is null || envelope.Stories.Count == 0)
            return null;

        _localCache = envelope;
        return ToCachedStories(envelope, now);
    }

    public async Task SetTopStoriesAsync(IReadOnlyList<StoryResponseDto> stories, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var envelope = new CacheEnvelope(
            stories,
            now.AddMinutes(_options.BestStoriesTopTtlMinutes),
            now.AddMinutes(_options.StaleCacheTtlMinutes));

        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.StaleCacheTtlMinutes)
        };

        _localCache = envelope;
        var json = JsonSerializer.Serialize(envelope, _jsonOptions);
        await _cache.SetStringAsync(TopStoriesKey, json, cacheOptions, cancellationToken);
        _logger.LogInformation("Top stories written to Redis. StoryCount={StoryCount}", stories.Count);
    }

    private bool IsUsable(CacheEnvelope? envelope, DateTimeOffset now)
    {
        if (envelope?.Stories is null || envelope.Stories.Count == 0)
            return false;

        return now <= GetStaleUntilUtc(envelope);
    }

    private CachedStories ToCachedStories(CacheEnvelope envelope, DateTimeOffset now)
    {
        return new CachedStories(envelope.Stories, now > envelope.FreshUntilUtc);
    }

    private DateTimeOffset GetStaleUntilUtc(CacheEnvelope envelope)
    {
        return envelope.StaleUntilUtc ?? envelope.FreshUntilUtc.AddMinutes(_options.StaleCacheTtlMinutes);
    }

    private sealed record CacheEnvelope(
        IReadOnlyList<StoryResponseDto> Stories,
        DateTimeOffset FreshUntilUtc,
        DateTimeOffset? StaleUntilUtc = null);
}
