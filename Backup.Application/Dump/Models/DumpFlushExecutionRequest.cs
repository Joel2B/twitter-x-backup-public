namespace Backup.Application.Dump.Models;

public sealed class DumpFlushExecutionRequest
{
    public required string UserId { get; init; }
    public required string Type { get; init; }
    public required string ContextId { get; init; }
    public required IReadOnlyList<Backup.Domain.Posts.Post> Posts { get; init; }
}
