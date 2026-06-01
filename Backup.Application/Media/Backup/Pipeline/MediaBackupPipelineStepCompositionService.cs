using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupPipelineStepCompositionService
    : IMediaBackupPipelineStepCompositionService
{
    public IReadOnlyList<MediaBackupPhaseStep> BuildPhaseSteps(
        IEnumerable<MediaBackupPipelineStepDescriptorInput> steps
    ) =>
        steps
            .Select(step => new MediaBackupPhaseStep
            {
                StepId = step.StepId,
                Order = step.Order,
                TimerName = step.TimerName,
                SkipWhenStopped = step.SkipWhenStopped,
            })
            .ToList();
}
