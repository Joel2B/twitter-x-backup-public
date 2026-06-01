namespace Backup.Application.Media.Maintenance;

public interface IMediaMaintenanceIntegrityPolicyService
{
    bool ShouldRemoveFromIntegrity(
        long? cacheFileSize,
        bool isValidMediaFile,
        long maxFileSizeBytes
    );
}
