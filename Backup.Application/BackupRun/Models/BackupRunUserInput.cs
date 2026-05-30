using Backup.Domain.BackupRun;

namespace Backup.Application.BackupRun.Models;

public sealed class BackupRunUserInput
{
    public required string UserId { get; init; }
    public required IReadOnlyDictionary<string, BackupRunApiPlan> Api { get; init; }
}
