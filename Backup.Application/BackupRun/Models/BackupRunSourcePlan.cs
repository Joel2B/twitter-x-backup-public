namespace Backup.Application.BackupRun.Models;

public sealed class BackupRunSourcePlan
{
    public required string SourceId { get; init; }
    public required int Count { get; init; }
}
