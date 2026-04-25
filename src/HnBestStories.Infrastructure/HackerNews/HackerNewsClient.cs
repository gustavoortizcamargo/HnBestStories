using System.Net.Http.Json;
using HnBestStories.Application.Dtos;
using HnBestStories.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace HnBestStories.Infrastructure.HackerNews;

public sealed class HackerNewsClient : IHackerNewsGateway
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HackerNewsClient> _logger;

    public HackerNewsClient(HttpClient httpClient, ILogger<HackerNewsClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<int>> GetBestStoryIdsAsync(CancellationToken cancellationToken)
    {
        var ids = await _httpClient.GetFromJsonAsync<IReadOnlyList<int>>("beststories.json", cancellationToken);
        return ids ?? Array.Empty<int>();
    }

    public async Task<HackerNewsItemDto?> GetItemAsync(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
            return null;

        try
        {
            return await _httpClient.GetFromJsonAsync<HackerNewsItemDto>($"item/{id}.json", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch Hacker News item. StoryId={StoryId}", id);
            throw;
        }
    }
}
