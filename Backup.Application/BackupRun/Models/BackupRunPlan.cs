namespace Backup.Application.BackupRun.Models;

public sealed class BackupRunPlan
{
    public required IReadOnlyList<BackupRunUserPlan> Users { get; init; }
    public required bool IsBulkEnabled { get; init; }
    public required bool IsMediaEnabled { get; init; }
}
