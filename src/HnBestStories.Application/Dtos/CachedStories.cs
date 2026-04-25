namespace HnBestStories.Application.Dtos;

public sealed record CachedStories(IReadOnlyList<StoryResponseDto> Stories, bool IsStale);
