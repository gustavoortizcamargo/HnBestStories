using HnBestStories.Application.Interfaces;
using HnBestStories.Application.Services;
using HnBestStories.Infrastructure.Cache;
using HnBestStories.Infrastructure.HackerNews;
using HnBestStories.Infrastructure.Options;
using HnBestStories.Infrastructure.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

namespace HnBestStories.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<HackerNewsOptions>(configuration.GetSection(HackerNewsOptions.SectionName));
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.SectionName));
        services.Configure<ResilienceOptions>(configuration.GetSection(ResilienceOptions.SectionName));

        var redisOptions = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>() ?? new RedisOptions();
        services.AddStackExchangeRedisCache(options => options.Configuration = redisOptions.ConnectionString);

        services.AddSingleton<IBestStoriesService, BestStoriesService>();
        services.AddSingleton<ICacheWarmupService>(sp => (BestStoriesService)sp.GetRequiredService<IBestStoriesService>());
        services.AddSingleton<IStoryCache, RedisStoryCache>();

        services.AddHttpClient<IHackerNewsGateway, HackerNewsClient>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<HackerNewsOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(sp.GetRequiredService<IOptions<ResilienceOptions>>().Value.TimeoutSeconds);
            })
            .AddPolicyHandler((sp, _) =>
            {
                var logger = sp.GetRequiredService<ILogger<HackerNewsClient>>();
                var options = sp.GetRequiredService<IOptions<ResilienceOptions>>().Value;
                return ResiliencePolicies.GetRetryPolicy(logger, options.RetryCount);
            })
            .AddPolicyHandler((sp, _) =>
            {
                var logger = sp.GetRequiredService<ILogger<HackerNewsClient>>();
                var options = sp.GetRequiredService<IOptions<ResilienceOptions>>().Value;
                return ResiliencePolicies.GetCircuitBreakerPolicy(
                    logger,
                    options.CircuitBreakerHandledEventsAllowedBeforeBreaking,
                    options.CircuitBreakerDurationSeconds);
            });

        return services;
    }
}
