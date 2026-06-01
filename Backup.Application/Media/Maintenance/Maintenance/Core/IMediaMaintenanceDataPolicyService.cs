namespace Backup.Application.Media.Maintenance;

public interface IMediaMaintenanceDataPolicyService
{
    bool ShouldRemoveCachedDownload(long? cacheFileSize);
}
