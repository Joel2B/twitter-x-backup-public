namespace Backup.Domain.BackupRun;

public sealed class BackupRunUserPlan
{
    public required string UserId { get; init; }
    public required IReadOnlyDictionary<string, BackupRunApiPlan> Api { get; init; }
    public required IReadOnlyList<BackupRunSourcePlan> Sources { get; init; }
    public required bool RunRecovery { get; init; }
    public required bool RunBulk { get; init; }
}
