using HnBestStories.Domain.Exceptions;
using HnBestStories.Domain.ValueObjects;

namespace HnBestStories.Domain.Entities;

public sealed class Story
{
    private Story(StoryId id, string title, string uri, string postedBy, DateTimeOffset time, int score, int commentCount)
    {
        Id = id;
        Title = title;
        Uri = uri;
        PostedBy = postedBy;
        Time = time.ToUniversalTime();
        Score = score;
        CommentCount = commentCount;
    }

    public StoryId Id { get; }
    public string Title { get; }
    public string Uri { get; }
    public string PostedBy { get; }
    public DateTimeOffset Time { get; }
    public int Score { get; }
    public int CommentCount { get; }

    public static Story Create(int id, string? title, string? uri, string? postedBy, DateTimeOffset time, int score, int commentCount)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Story title cannot be empty.");

        if (string.IsNullOrWhiteSpace(uri))
            throw new DomainException("Story uri cannot be empty.");

        if (postedBy is null)
            throw new DomainException("Story postedBy cannot be null.");

        if (score < 0)
            throw new DomainException("Story score cannot be negative.");

        if (commentCount < 0)
            throw new DomainException("Story comment count cannot be negative.");

        return new Story(new StoryId(id), title.Trim(), uri.Trim(), postedBy.Trim(), time.ToUniversalTime(), score, commentCount);
    }
}
