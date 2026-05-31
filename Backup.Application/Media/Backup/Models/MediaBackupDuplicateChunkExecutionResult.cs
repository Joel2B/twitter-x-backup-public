namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupDuplicateChunkExecutionResult
{
    public required IReadOnlyList<string> MemoryArchivePaths { get; init; }

    public required IReadOnlyList<string> StorageArchivePaths { get; init; }

    public required MediaBackupDuplicateChunkExecutionPlan ExecutionPlan { get; init; }

    public required int RemovedExtrasCount { get; init; }
}
