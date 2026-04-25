using FluentAssertions;
using HnBestStories.Application.Dtos;
using HnBestStories.Application.Interfaces;
using HnBestStories.Application.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace HnBestStories.UnitTests;

public sealed class BestStoriesServiceTests
{
    [Fact]
    public async Task GetBestStories_Should_Order_By_Score_Descending()
    {
        var service = CreateService(new[]
        {
            Item(1, score: 10),
            Item(2, score: 50),
            Item(3, score: 20)
        });

        var result = await service.GetBestStoriesAsync(3, CancellationToken.None);

        result.Select(x => x.Score).Should().Equal(50, 20, 10);
    }

    [Fact]
    public async Task GetBestStories_Should_Respect_N()
    {
        var service = CreateService(new[]
        {
            Item(1, score: 10),
            Item(2, score: 50),
            Item(3, score: 20)
        });

        var result = await service.GetBestStoriesAsync(2, CancellationToken.None);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetBestStories_Should_Ignore_Deleted_Dead_And_NonStory_Items()
    {
        var service = CreateService(new[]
        {
            Item(1, score: 10),
            Item(2, score: 50, deleted: true),
            Item(3, score: 20, dead: true),
            Item(4, score: 80, type: "comment")
        });

        var result = await service.GetBestStoriesAsync(4, CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Title.Should().Be("Story 1");
    }

    [Fact]
    public async Task GetBestStories_Should_Use_HackerNews_Discussion_Url_When_Url_Is_Empty()
    {
        var service = CreateService(new[] { Item(42, score: 10, url: "") });

        var result = await service.GetBestStoriesAsync(1, CancellationToken.None);

        result[0].Uri.Should().Be("https://news.ycombinator.com/item?id=42");
    }

    private static BestStoriesService CreateService(IReadOnlyList<HackerNewsItemDto> items)
    {
        var gateway = Substitute.For<IHackerNewsGateway>();
        gateway.GetBestStoryIdsAsync(Arg.Any<CancellationToken>())
            .Returns(items.Select(x => x.Id).ToArray());

        foreach (var item in items)
        {
            gateway.GetItemAsync(item.Id, Arg.Any<CancellationToken>()).Returns(item);
        }

        var cache = Substitute.For<IStoryCache>();
        cache.GetTopStoriesAsync(Arg.Any<CancellationToken>()).Returns((CachedStories?)null);

        return new BestStoriesService(gateway, cache, NullLogger<BestStoriesService>.Instance);
    }

    private static HackerNewsItemDto Item(
        int id,
        int score,
        bool? deleted = null,
        bool? dead = null,
        string type = "story",
        string? url = "https://example.com")
    {
        return new HackerNewsItemDto
        {
            Id = id,
            Title = $"Story {id}",
            Url = url,
            By = "user",
            Time = 1_600_000_000,
            Score = score,
            Descendants = 1,
            Type = type,
            Deleted = deleted,
            Dead = dead
        };
    }
}
