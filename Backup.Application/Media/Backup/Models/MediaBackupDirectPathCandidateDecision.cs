namespace Backup.Application.Media.Backup.Models;

public sealed class MediaBackupDirectPathCandidateDecision
{
    public required bool ShouldThrowMissingSource { get; init; }

    public required bool ShouldIncludeDirectPath { get; init; }
}
