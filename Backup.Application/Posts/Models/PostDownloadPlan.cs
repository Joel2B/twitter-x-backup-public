namespace Backup.Application.Posts.Models;

public sealed class PostDownloadPlan
{
    public required int QueryCount { get; init; }
    public required int TotalCount { get; init; }
    public required int DownloadedCount { get; set; }
    public string? Cursor { get; set; }
}
