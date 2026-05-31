namespace Backup.Application.Media;

public interface IMediaOrchestrationStorageResolutionService
{
    IReadOnlyList<string> GetStorageIds(IEnumerable<string> storageIds);
    bool HasMaintenance(string storageId, IEnumerable<string> maintenanceIds);
    string? ResolveStorageId(string storageId, IEnumerable<string> storageIds);
    string? SelectBackupSourceId(IEnumerable<string> storageIds);
}
