using HnBestStories.Application.Dtos;

namespace HnBestStories.Application.Interfaces;

public interface IHackerNewsGateway
{
    Task<IReadOnlyList<int>> GetBestStoryIdsAsync(CancellationToken cancellationToken);
    Task<HackerNewsItemDto?> GetItemAsync(int id, CancellationToken cancellationToken);
}
