using FluentAssertions;
using HnBestStories.Domain.Entities;
using HnBestStories.Domain.Exceptions;

namespace HnBestStories.UnitTests;

public sealed class StoryTests
{
    [Fact]
    public void Create_Should_Throw_When_Id_Is_Invalid()
    {
        var act = () => Story.Create(0, "title", "https://example.com", "user", DateTimeOffset.UtcNow, 1, 0);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_Should_Throw_When_Title_Is_Empty()
    {
        var act = () => Story.Create(1, "", "https://example.com", "user", DateTimeOffset.UtcNow, 1, 0);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_Should_Throw_When_Score_Is_Negative()
    {
        var act = () => Story.Create(1, "title", "https://example.com", "user", DateTimeOffset.UtcNow, -1, 0);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_Should_Throw_When_CommentCount_Is_Negative()
    {
        var act = () => Story.Create(1, "title", "https://example.com", "user", DateTimeOffset.UtcNow, 1, -1);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_Should_Convert_Time_To_Utc()
    {
        var offsetTime = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.FromHours(-3));

        var story = Story.Create(1, "title", "https://example.com", "user", offsetTime, 1, 0);

        story.Time.Offset.Should().Be(TimeSpan.Zero);
        story.Time.Hour.Should().Be(13);
    }
}
