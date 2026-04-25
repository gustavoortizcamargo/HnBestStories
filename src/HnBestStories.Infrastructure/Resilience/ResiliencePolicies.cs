using System.Net;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;

namespace HnBestStories.Infrastructure.Resilience;

public static class ResiliencePolicies
{
    public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(ILogger logger, int retryCount)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(response => response.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount,
                retryAttempt => TimeSpan.FromMilliseconds(200 * Math.Pow(2, retryAttempt)),
                (outcome, delay, retryAttempt, _) =>
                {
                    logger.LogWarning("Retrying Hacker News call. RetryAttempt={RetryAttempt} DelayMs={DelayMs}", retryAttempt, delay.TotalMilliseconds);
                });
    }

    public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(ILogger logger, int handledEventsAllowedBeforeBreaking, int durationSeconds)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking,
                TimeSpan.FromSeconds(durationSeconds),
                (_, breakDelay) => logger.LogWarning("Hacker News circuit breaker opened. DurationSeconds={DurationSeconds}", breakDelay.TotalSeconds),
                () => logger.LogInformation("Hacker News circuit breaker closed."));
    }
}
