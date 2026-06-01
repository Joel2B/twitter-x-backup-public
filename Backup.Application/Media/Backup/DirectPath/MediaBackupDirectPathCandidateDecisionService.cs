using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupDirectPathCandidateDecisionService(
    IMediaBackupDirectPathEligibilityService directPathEligibilityService
) : IMediaBackupDirectPathCandidateDecisionService
{
    private readonly IMediaBackupDirectPathEligibilityService _directPathEligibilityService =
        directPathEligibilityService;

    public MediaBackupDirectPathCandidateDecision Decide(
        MediaBackupDirectPathCandidateObservation observation
    )
    {
        if (!observation.CacheExists)
        {
            return new MediaBackupDirectPathCandidateDecision
            {
                ShouldThrowMissingSource = false,
                ShouldIncludeDirectPath = false,
            };
        }

        if (
            observation.FileSizeBytes is null
            || observation.FileSizeBytes <= observation.MaxPathSizeBytes
        )
        {
            return new MediaBackupDirectPathCandidateDecision
            {
                ShouldThrowMissingSource = false,
                ShouldIncludeDirectPath = false,
            };
        }

        if (!observation.SourceExists)
        {
            return new MediaBackupDirectPathCandidateDecision
            {
                ShouldThrowMissingSource = true,
                ShouldIncludeDirectPath = false,
            };
        }

        bool shouldInclude = _directPathEligibilityService.ShouldBackupDirect(
            new MediaBackupDirectPathCandidate
            {
                CachePath = observation.CachePath,
                FileSizeBytes = observation.FileSizeBytes,
                TargetExists = observation.TargetExists,
            },
            observation.MaxPathSizeBytes
        );

        return new MediaBackupDirectPathCandidateDecision
        {
            ShouldThrowMissingSource = false,
            ShouldIncludeDirectPath = shouldInclude,
        };
    }
}
