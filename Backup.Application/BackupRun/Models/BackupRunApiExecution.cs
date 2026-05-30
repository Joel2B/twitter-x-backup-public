namespace Backup.Application.BackupRun.Models;

public sealed class BackupRunApiExecution
{
    public required string Id { get; init; }
    public required bool Enabled { get; init; }
    public required BackupRunRequestExecution Request { get; init; }
}
