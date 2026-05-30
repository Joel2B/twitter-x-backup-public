using Backup.Domain.Posts;

namespace Backup.Application.Posts.Models;

public sealed class PostDownloadPageResult
{
    public required IReadOnlyCollection<Post> Posts { get; init; }
    public required string RawResponse { get; init; }
    public string? NextCursor { get; init; }

    public bool HasValidPage => Posts.Count > 0 && NextCursor is not null;
}

