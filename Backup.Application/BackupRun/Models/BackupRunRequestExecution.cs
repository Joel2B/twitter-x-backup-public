namespace Backup.Application.BackupRun.Models;

public sealed class BackupRunRequestExecution
{
    public required string Url { get; init; }
    public required IReadOnlyDictionary<string, object?> Variables { get; init; }
    public required IReadOnlyDictionary<string, bool> Features { get; init; }
    public required IReadOnlyDictionary<string, bool> FieldToggles { get; init; }
    public required IReadOnlyDictionary<string, string> Headers { get; init; }
}
