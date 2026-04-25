using HnBestStories.Application.Dtos;
using HnBestStories.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HnBestStories.Api.Extensions;

public static class EndpointExtensions
{
    public static IEndpointRouteBuilder MapStoryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/stories/best", async (
                [FromQuery] int? n,
                IBestStoriesService service,
                CancellationToken cancellationToken) =>
            {
                if (n is null)
                {
                    return Results.BadRequest(new ProblemDetails
                    {
                        Title = "Invalid request.",
                        Detail = "Query parameter 'n' is required.",
                        Status = StatusCodes.Status400BadRequest
                    });
                }

                if (n <= 0 || n > 500)
                {
                    return Results.BadRequest(new ProblemDetails
                    {
                        Title = "Invalid request.",
                        Detail = "Query parameter 'n' must be between 1 and 500.",
                        Status = StatusCodes.Status400BadRequest
                    });
                }

                var stories = await service.GetBestStoriesAsync(n.Value, cancellationToken);
                return Results.Ok(stories);
            })
            .WithName("GetBestStories")            
            .Produces<IReadOnlyList<StoryResponseDto>>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)
            .RequireRateLimiting("fixed");

        return app;
    }
}
