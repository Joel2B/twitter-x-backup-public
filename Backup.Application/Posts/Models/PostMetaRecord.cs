namespace Backup.Application.Posts.Models;

public sealed class PostMetaRecord
{
    public required string Id { get; init; }
    public required string Hash { get; init; }
    public required bool Deleted { get; init; }
}
