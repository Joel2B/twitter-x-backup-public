namespace Backup.Application.Media.Maintenance;

public interface IMediaMaintenanceFileProbePolicyService
{
    bool ShouldProbe(long? cacheFileSize, long maxFileSizeBytes);
}
