namespace Backup.Application.BackupRun.Models;

public sealed class BackupRunPlanInput
{
    public required IReadOnlyList<BackupRunUserInput> Users { get; init; }
    public required IReadOnlyDictionary<string, BackupRunFetchInput> Fetch { get; init; }
    public required bool IsBulkEnabled { get; init; }
    public required bool IsMediaEnabled { get; init; }
}
