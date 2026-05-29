namespace Backup.Application.BackupRun.Models;

public sealed class BackupRunApiPlan
{
    public required string Id { get; init; }
    public required bool Enabled { get; init; }
    public required BackupRunRequestPlan Request { get; init; }
}
