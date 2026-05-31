namespace Backup.Application.Dump.Models;

public sealed class DumpFlushExecutionResult
{
    public required string SourceId { get; init; }
    public required int LoadedCount { get; init; }
    public required int DeletedCount { get; init; }
    public required IReadOnlyCollection<string> NewPostIds { get; init; }
}
