using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupDirectPathScanOrchestrationService(
    IMediaBackupDirectPathCandidateDecisionService mediaBackupDirectPathCandidateDecisionService
) : IMediaBackupDirectPathScanOrchestrationService
{
    private readonly IMediaBackupDirectPathCandidateDecisionService _mediaBackupDirectPathCandidateDecisionService =
        mediaBackupDirectPathCandidateDecisionService;

    public MediaBackupDirectPathScanResult Evaluate(
        MediaBackupDirectPathCandidateObservation observation
    )
    {
        MediaBackupDirectPathCandidateDecision decision =
            _mediaBackupDirectPathCandidateDecisionService.Decide(observation);

        return new MediaBackupDirectPathScanResult
        {
            ShouldThrowMissingSource = decision.ShouldThrowMissingSource,
            ShouldIncludeDirectPath = decision.ShouldIncludeDirectPath,
            IncludedPath = observation.CachePath,
        };
    }
}
