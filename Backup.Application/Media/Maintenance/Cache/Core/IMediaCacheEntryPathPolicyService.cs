namespace Backup.Application.Media.Maintenance;

public interface IMediaCacheEntryPathPolicyService
{
    string NormalizeForCacheKey(string path);
    string NormalizeForStoragePath(string path);
    string BuildCacheSnapshotFileName(string normalizedPath, int? partitionId);
}
