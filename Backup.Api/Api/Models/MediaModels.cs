namespace Backup.Api.Models;

public sealed class MediaStorageSummary
{
    public required string StorageId { get; init; }
    public required bool HasMaintenance { get; init; }
}
