namespace Backup.Application.Posts.Models;

public sealed class PostComputedChangeField
{
    public required string Field { get; init; }
    public string? OldValueJson { get; init; }
    public string? NewValueJson { get; init; }
}
