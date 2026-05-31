namespace Backup.Application.Posts.Models;

public sealed class PostComputedChange
{
    public required string UserId { get; init; }
    public DateTime Date { get; init; }
    public required string ChangeType { get; init; }
    public required IReadOnlyList<PostComputedChangeField> Fields { get; init; }
}
