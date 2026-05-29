namespace Backup.Domain.BackupRun;

public sealed class BackupRunSourcePlan
{
    public required string SourceId { get; init; }
    public required string ApiId { get; init; }
    public required int Count { get; init; }
    public required BackupRunRequestPlan Request { get; init; }
}
