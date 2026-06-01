using Backup.Application.Dump.Models;

namespace Backup.Application.Dump;

public sealed class DumpFlushRequestFactoryService : IDumpFlushRequestFactoryService
{
    public DumpFlushExecutionRequest Build(
        string userId,
        string? dumpType,
        string contextId,
        IReadOnlyList<Backup.Domain.Posts.Post> posts
    ) =>
        new()
        {
            UserId = userId,
            Type = dumpType ?? string.Empty,
            ContextId = contextId,
            Posts = posts,
        };
}
