namespace Backup.Application.Media.Maintenance;

public sealed class MediaMaintenanceFileProbePolicyService
    : IMediaMaintenanceFileProbePolicyService
{
    public bool ShouldProbe(long? cacheFileSize, long maxFileSizeBytes) =>
        cacheFileSize is long size && size < maxFileSizeBytes;
}
