using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public sealed class MediaMaintenanceIntegrityEvaluationService(
    IMediaMaintenanceIntegrityPolicyService mediaMaintenanceIntegrityPolicyService
) : IMediaMaintenanceIntegrityEvaluationService
{
    private readonly IMediaMaintenanceIntegrityPolicyService _mediaMaintenanceIntegrityPolicyService =
        mediaMaintenanceIntegrityPolicyService;

    public MediaMaintenanceIntegrityEvaluation Evaluate(
        long? cacheFileSize,
        bool isValidMediaFile,
        long maxFileSizeBytes
    )
    {
        bool remove = _mediaMaintenanceIntegrityPolicyService.ShouldRemoveFromIntegrity(
            cacheFileSize,
            isValidMediaFile,
            maxFileSizeBytes
        );

        return new MediaMaintenanceIntegrityEvaluation
        {
            Remove = remove,
            NullCountIncrement = cacheFileSize is null ? 1 : 0,
            SizeCountIncrement = cacheFileSize is null ? 0 : 1,
            InvalidCountIncrement = !remove && !isValidMediaFile ? 1 : 0,
        };
    }
}
