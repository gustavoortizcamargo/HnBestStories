namespace HnBestStories.Application.Exceptions;

public sealed class HackerNewsUnavailableException : Exception
{
    public HackerNewsUnavailableException(string message, Exception? innerException = null) : base(message, innerException) { }
}
