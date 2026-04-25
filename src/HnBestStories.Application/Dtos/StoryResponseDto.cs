namespace HnBestStories.Application.Dtos;

public sealed record StoryResponseDto(
    string Title,
    string Uri,
    string PostedBy,
    DateTimeOffset Time,
    int Score,
    int CommentCount);
