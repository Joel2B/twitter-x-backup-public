namespace Backup.Application.Posts.Models;

public sealed class PostChangeReplayEntry
{
    public required string UserId { get; init; }
    public required DateTime Date { get; init; }
    public required long Sequence { get; init; }
    public required IReadOnlyList<PostChangeReplayField> Fields { get; init; }
}

public sealed class PostChangeReplayField
{
    public required string Field { get; init; }
    public string? OldValueJson { get; init; }
}
