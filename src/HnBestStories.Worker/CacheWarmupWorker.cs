using HnBestStories.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace HnBestStories.Worker;

public sealed class CacheWarmupWorker : BackgroundService
{
    private readonly ICacheWarmupService _cacheWarmupService;
    private readonly ILogger<CacheWarmupWorker> _logger;
    private readonly WorkerOptions _options;

    public CacheWarmupWorker(
        ICacheWarmupService cacheWarmupService,
        IOptions<WorkerOptions> options,
        ILogger<CacheWarmupWorker> logger)
    {
        _cacheWarmupService = cacheWarmupService;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Cache warmup worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var startedAt = DateTimeOffset.UtcNow;
                await _cacheWarmupService.WarmupAsync(stoppingToken);
                _logger.LogInformation("Cache warmup completed. DurationMs={DurationMs}", (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cache warmup failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(_options.RefreshIntervalMinutes), stoppingToken);
        }
    }
}
