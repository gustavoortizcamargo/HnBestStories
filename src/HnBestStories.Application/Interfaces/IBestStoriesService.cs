using HnBestStories.Application.Dtos;

namespace HnBestStories.Application.Interfaces;

public interface IBestStoriesService
{
    Task<IReadOnlyList<StoryResponseDto>> GetBestStoriesAsync(int count, CancellationToken cancellationToken);
}
