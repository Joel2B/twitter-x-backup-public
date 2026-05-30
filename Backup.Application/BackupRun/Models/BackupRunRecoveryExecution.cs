namespace Backup.Application.BackupRun.Models;

public sealed class BackupRunRecoveryExecution
{
    public required string UserId { get; init; }
    public required IReadOnlyDictionary<string, BackupRunApiExecution> Api { get; init; }
}
