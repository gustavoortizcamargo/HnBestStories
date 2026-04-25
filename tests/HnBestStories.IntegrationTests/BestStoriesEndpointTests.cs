using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Testcontainers.Redis;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace HnBestStories.IntegrationTests;

public sealed class BestStoriesEndpointTests
{
    [Fact]
    public async Task GetBestStories_Returns_Requested_Stories_Ordered_By_Score()
    {
        await using var factory = await HnBestStoriesApiFactory.StartAsync();
        factory.StubBestStories(1, 2, 3);
        factory.StubStory(1, "Lowest score", score: 10);
        factory.StubStory(2, "Highest score", score: 50);
        factory.StubStory(3, "Middle score", score: 30);

        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/stories/best?n=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stories = await response.Content.ReadFromJsonAsync<List<StoryResponse>>();

        stories.Should().NotBeNull();
        stories!.Should().HaveCount(2);
        stories.Select(story => story.Title).Should().Equal("Highest score", "Middle score");
        stories.Select(story => story.Score).Should().Equal(50, 30);
        stories[0].Uri.Should().Be("https://example.com/story-2");
        stories[0].PostedBy.Should().Be("user-2");
        stories[0].CommentCount.Should().Be(5);
    }

    [Theory]
    [InlineData("/api/stories/best")]
    [InlineData("/api/stories/best?n=0")]
    [InlineData("/api/stories/best?n=501")]
    public async Task GetBestStories_Returns_BadRequest_For_Invalid_N(string url)
    {
        await using var factory = new HnBestStoriesValidationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetBestStories_Uses_Cache_For_Subsequent_Requests()
    {
        await using var factory = await HnBestStoriesApiFactory.StartAsync();
        factory.StubBestStories(1, 2);
        factory.StubStory(1, "First story", score: 20);
        factory.StubStory(2, "Second story", score: 10);

        using var client = factory.CreateClient();

        var firstResponse = await client.GetAsync("/api/stories/best?n=2");
        var secondResponse = await client.GetAsync("/api/stories/best?n=2");

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        factory.CountHackerNewsRequestsTo("/v0/beststories.json").Should().Be(1);
        factory.CountHackerNewsRequestsTo("/v0/item/1.json").Should().Be(1);
        factory.CountHackerNewsRequestsTo("/v0/item/2.json").Should().Be(1);
    }

    private sealed record StoryResponse(
        string Title,
        string Uri,
        string PostedBy,
        DateTimeOffset Time,
        int Score,
        int CommentCount);
}

public sealed class HnBestStoriesApiFactory : WebApplicationFactory<Program>
{
    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    private WireMockServer? _hackerNews;

    public WireMockServer HackerNews => _hackerNews ?? throw new InvalidOperationException("WireMock server was not started.");

    public static async Task<HnBestStoriesApiFactory> StartAsync()
    {
        var factory = new HnBestStoriesApiFactory();
        await factory.InitializeAsync();
        return factory;
    }

    private async Task InitializeAsync()
    {
        _hackerNews = WireMockServer.Start();
        await _redis.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        _hackerNews?.Stop();
        _hackerNews?.Dispose();
        await _redis.DisposeAsync();
        await base.DisposeAsync();
    }

    public void StubBestStories(params int[] ids)
    {
        HackerNews
            .Given(Request.Create().WithPath("/v0/beststories.json").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(ids));
    }

    public void StubStory(int id, string title, int score)
    {
        HackerNews
            .Given(Request.Create().WithPath($"/v0/item/{id}.json").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    id,
                    type = "story",
                    by = $"user-{id}",
                    time = 1_600_000_000,
                    url = $"https://example.com/story-{id}",
                    title,
                    score,
                    descendants = 5
                }));
    }

    public int CountHackerNewsRequestsTo(string path)
    {
        return HackerNews.LogEntries.Count(entry =>
            string.Equals(entry.RequestMessage.Path, path, StringComparison.OrdinalIgnoreCase));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HackerNews:BaseUrl"] = $"{HackerNews.Urls[0]}/v0/",
                ["Redis:ConnectionString"] = _redis.GetConnectionString(),
                ["Cache:BestStoriesTopTtlMinutes"] = "30",
                ["Cache:StaleCacheTtlMinutes"] = "60",
                ["Seq:ServerUrl"] = "http://localhost:5341"
            });
        });
    }
}

public sealed class HnBestStoriesValidationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HackerNews:BaseUrl"] = "http://localhost/v0/",
                ["Redis:ConnectionString"] = "localhost:6379",
                ["Seq:ServerUrl"] = "http://localhost:5341"
            });
        });
    }
}
