namespace Backup.Domain.BackupRun;

public sealed class BackupRunRequestPlan
{
    public required string Url { get; init; }
    public required Dictionary<string, object?> Variables { get; init; }
    public required Dictionary<string, bool> Features { get; init; }
    public required Dictionary<string, bool> FieldToggles { get; init; }
    public required Dictionary<string, string> Headers { get; init; }
}
