using HnBestStories.Domain.Exceptions;

namespace HnBestStories.Domain.ValueObjects;

public readonly record struct StoryId
{
    public int Value { get; }

    public StoryId(int value)
    {
        if (value <= 0)
            throw new DomainException("Story id must be greater than zero.");

        Value = value;
    }

    public override string ToString() => Value.ToString();
}
