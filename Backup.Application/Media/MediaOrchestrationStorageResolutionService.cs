namespace Backup.Application.Media;

public sealed class MediaOrchestrationStorageResolutionService
    : IMediaOrchestrationStorageResolutionService
{
    public IReadOnlyList<string> GetStorageIds(IEnumerable<string> storageIds) =>
        storageIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public bool HasMaintenance(string storageId, IEnumerable<string> maintenanceIds) =>
        maintenanceIds.Contains(storageId, StringComparer.OrdinalIgnoreCase);

    public string? ResolveStorageId(string storageId, IEnumerable<string> storageIds) =>
        storageIds.FirstOrDefault(id => string.Equals(id, storageId, StringComparison.OrdinalIgnoreCase));

    public string? SelectBackupSourceId(IEnumerable<string> storageIds) =>
        GetStorageIds(storageIds).FirstOrDefault();
}
