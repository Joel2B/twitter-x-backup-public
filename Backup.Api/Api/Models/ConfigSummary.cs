namespace Backup.Api.Models;

public sealed class ConfigSummary
{
    public required long Version { get; init; }
    public required DateTimeOffset LoadedAt { get; init; }
    public required IReadOnlyList<ConfigUserSummary> Users { get; init; }
    public required IReadOnlyList<StoreSummary> PostStores { get; init; }
    public required IReadOnlyList<StoreSummary> DumpStores { get; init; }
    public required IReadOnlyList<StoreSummary> BulkStores { get; init; }
    public required IReadOnlyList<StoreSummary> MediaStores { get; init; }
    public required IReadOnlyList<StoreSummary> BackupStores { get; init; }
    public required IReadOnlyList<PartitionSummary> Partitions { get; init; }
    public required IReadOnlyDictionary<string, int> FetchCounts { get; init; }
    public required bool BulkEnabled { get; init; }
    public required bool MediaEnabled { get; init; }
}

public sealed class ConfigStoresSummary
{
    public required IReadOnlyList<StoreSummary> PostStores { get; init; }
    public required IReadOnlyList<StoreSummary> DumpStores { get; init; }
    public required IReadOnlyList<StoreSummary> BulkStores { get; init; }
    public required IReadOnlyList<StoreSummary> MediaStores { get; init; }
    public required IReadOnlyList<StoreSummary> BackupStores { get; init; }
    public required IReadOnlyList<PartitionSummary> Partitions { get; init; }
}

public sealed class ConfigUserSummary
{
    public required string UserId { get; init; }
    public required IReadOnlyList<ConfigUserSourceSummary> Sources { get; init; }
}

public sealed class ConfigUserSourceSummary
{
    public required string SourceId { get; init; }
    public required string ApiId { get; init; }
    public required bool Enabled { get; init; }
}

public sealed class StoreSummary
{
    public required string? Id { get; init; }
    public required string Type { get; init; }
    public required bool Enabled { get; init; }
    public required bool IsDefault { get; init; }
    public required IReadOnlyList<int> Partitions { get; init; }
}

public sealed class PartitionSummary
{
    public required int Id { get; init; }
    public required string? Name { get; init; }
    public required string Type { get; init; }
    public required bool Enabled { get; init; }
    public required long Size { get; init; }
    public required long UsableSpace { get; init; }
    public required IReadOnlyList<string> Paths { get; init; }
    public required IReadOnlyList<string>? Tags { get; init; }
}
