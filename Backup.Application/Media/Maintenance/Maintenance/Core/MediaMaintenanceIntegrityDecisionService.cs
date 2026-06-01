using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public sealed class MediaMaintenanceIntegrityDecisionService(
    IMediaMaintenanceFileProbePolicyService mediaMaintenanceFileProbePolicyService,
    IMediaMaintenanceIntegrityEvaluationService mediaMaintenanceIntegrityEvaluationService
) : IMediaMaintenanceIntegrityDecisionService
{
    private const long IntegritySizeThreshold = 1000;

    private readonly IMediaMaintenanceFileProbePolicyService _mediaMaintenanceFileProbePolicyService =
        mediaMaintenanceFileProbePolicyService;
    private readonly IMediaMaintenanceIntegrityEvaluationService _mediaMaintenanceIntegrityEvaluationService =
        mediaMaintenanceIntegrityEvaluationService;

    public bool ShouldProbe(long? sizeBytes) =>
        _mediaMaintenanceFileProbePolicyService.ShouldProbe(sizeBytes, IntegritySizeThreshold);

    public MediaMaintenanceIntegrityEvaluation Evaluate(long? sizeBytes, bool isValid) =>
        _mediaMaintenanceIntegrityEvaluationService.Evaluate(
            sizeBytes,
            isValid,
            IntegritySizeThreshold
        );
}
