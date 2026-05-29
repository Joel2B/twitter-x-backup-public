namespace Backup.Domain.BackupRun;

public sealed class BackupRunApiPlan
{
    public required string Id { get; init; }
    public required bool Enabled { get; init; }
    public required BackupRunRequestPlan Request { get; init; }
}
