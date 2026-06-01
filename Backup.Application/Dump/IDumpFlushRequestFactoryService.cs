using Backup.Application.Dump.Models;

namespace Backup.Application.Dump;

public interface IDumpFlushRequestFactoryService
{
    DumpFlushExecutionRequest Build(
        string userId,
        string? dumpType,
        string contextId,
        IReadOnlyList<Backup.Domain.Posts.Post> posts
    );
}
