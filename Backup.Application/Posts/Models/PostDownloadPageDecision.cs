namespace Backup.Application.Posts.Models;

public enum PostDownloadPageOutcome
{
    Retry,
    Success,
    Abort,
}

public sealed class PostDownloadPageDecision
{
    public required PostDownloadPageOutcome Outcome { get; init; }
    public bool ShouldFlushDump { get; init; }
}
