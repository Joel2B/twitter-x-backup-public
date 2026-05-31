using Backup.Application.Dump.Models;

namespace Backup.Application.Dump;

public interface IDumpFlushExecutionService
{
    Task<DumpFlushExecutionResult> Execute(
        DumpFlushExecutionRequest request,
        Func<string, IReadOnlyList<Backup.Domain.Posts.Post>, Task> addPosts,
        Func<string, IReadOnlyCollection<string>, Task<int>> markDeletedExcept
    );
}
