namespace Backup.Application.Media.Maintenance;

public sealed class MediaMaintenanceDataPolicyService : IMediaMaintenanceDataPolicyService
{
    public bool ShouldRemoveCachedDownload(long? cacheFileSize) =>
        cacheFileSize is long size && size != 0;
}
