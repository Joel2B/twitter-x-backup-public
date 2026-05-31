namespace Backup.Application.Posts.Models;

public sealed class PostProjectionParseBatchResult
{
    public required IReadOnlyList<ParsedPostProjection> Posts { get; init; }

    public required IReadOnlyList<string> Errors { get; init; }
}
