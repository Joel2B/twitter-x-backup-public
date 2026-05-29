namespace Backup.Application.Posts.Models;

public sealed class PostDownloadResumePoint
{
    public required int QueryCount { get; init; }
    public required int TotalCount { get; init; }
    public string? Cursor { get; init; }
}
