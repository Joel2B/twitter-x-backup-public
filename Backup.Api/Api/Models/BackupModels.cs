namespace Backup.Api.Models;

public sealed class BackupPlanResponse
{
    public required DateTimeOffset GeneratedAt { get; init; }
    public required bool IsBulkEnabled { get; init; }
    public required bool IsMediaEnabled { get; init; }
    public required IReadOnlyList<BackupPlanUserSummary> Users { get; init; }
}

public sealed class BackupPlanUserSummary
{
    public required string UserId { get; init; }
    public required bool RunRecovery { get; init; }
    public required bool RunBulk { get; init; }
    public required IReadOnlyDictionary<string, BackupPlanApiSummary> Api { get; init; }
    public required IReadOnlyList<BackupPlanSourceSummary> Sources { get; init; }
}

public sealed class BackupPlanApiSummary
{
    public required string Id { get; init; }
    public required bool Enabled { get; init; }
    public required BackupRequestSummary Request { get; init; }
}

public sealed class BackupPlanSourceSummary
{
    public required string SourceId { get; init; }
    public required string ApiId { get; init; }
    public required int Count { get; init; }
    public required BackupRequestSummary Request { get; init; }
}

public sealed class BackupRequestSummary
{
    public required string Url { get; init; }
    public required IReadOnlyDictionary<string, object?> Variables { get; init; }
    public required IReadOnlyDictionary<string, bool> Features { get; init; }
    public required IReadOnlyDictionary<string, bool> FieldToggles { get; init; }
    public required IReadOnlyDictionary<string, string> Headers { get; init; }
}
