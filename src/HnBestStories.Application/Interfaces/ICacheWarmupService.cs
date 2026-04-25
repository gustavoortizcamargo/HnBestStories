namespace HnBestStories.Application.Interfaces;

public interface ICacheWarmupService
{
    Task WarmupAsync(CancellationToken cancellationToken);
}
