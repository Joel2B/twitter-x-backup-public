using Backup.Application.Dump.Models;

namespace Backup.Application.Dump;

public interface IDumpFlushOrchestrationService
{
    Task<DumpFlushOrchestrationResult> ExecuteAsync(
        string userId,
        string dataType,
        string sourceId,
        string currentSession,
        IReadOnlyList<Backup.Domain.Posts.Post> domainPosts,
        Func<string, IReadOnlyList<Backup.Domain.Posts.Post>, Task> addPosts,
        Func<string, IReadOnlyCollection<string>, Task<int>> markDeletedExcept,
        CancellationToken cancellationToken = default
    );
}
