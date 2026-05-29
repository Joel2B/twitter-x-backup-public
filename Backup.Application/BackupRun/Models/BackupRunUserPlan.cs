namespace Backup.Application.BackupRun.Models;

public sealed class BackupRunUserPlan
{
    public required string UserId { get; init; }
    public required IReadOnlyList<BackupRunSourcePlan> Sources { get; init; }
    public required bool RunRecovery { get; init; }
    public required bool RunBulk { get; init; }
}
