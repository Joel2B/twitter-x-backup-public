namespace Backup.Application.Posts.Models;

public sealed class PostRecoveryLog
{
    public required string PostId { get; init; }
    public required IReadOnlyCollection<string> Messages { get; init; }
}
