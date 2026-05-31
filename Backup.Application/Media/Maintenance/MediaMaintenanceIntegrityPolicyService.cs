namespace Backup.Application.Media.Maintenance;

public sealed class MediaMaintenanceIntegrityPolicyService
    : IMediaMaintenanceIntegrityPolicyService
{
    public bool ShouldRemoveFromIntegrity(
        long? cacheFileSize,
        bool isValidMediaFile,
        long maxFileSizeBytes
    )
    {
        if (cacheFileSize is null)
            return true;

        if (cacheFileSize >= maxFileSizeBytes)
            return true;

        return isValidMediaFile;
    }
}
