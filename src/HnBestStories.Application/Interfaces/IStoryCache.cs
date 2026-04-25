using HnBestStories.Application.Dtos;

namespace HnBestStories.Application.Interfaces;

public interface IStoryCache
{
    Task<CachedStories?> GetTopStoriesAsync(CancellationToken cancellationToken);
    Task SetTopStoriesAsync(IReadOnlyList<StoryResponseDto> stories, CancellationToken cancellationToken);
}
