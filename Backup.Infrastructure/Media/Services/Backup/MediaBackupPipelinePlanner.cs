using Backup.Application.Media.Backup;
using Backup.Application.Media.Backup.Models;
using Backup.Infrastructure.Media.Abstractions.Services;

namespace Backup.Infrastructure.Media.Services;

internal sealed class MediaBackupPipelinePlanner(
    IMediaBackupPhaseOrchestrationService phaseOrchestrationService
)
{
    private readonly IMediaBackupPhaseOrchestrationService _phaseOrchestrationService =
        phaseOrchestrationService;

    public IReadOnlyList<MediaBackupPhaseExecutionStep> BuildExecutionPlan(
        IEnumerable<IMediaBackupPipelineStep> steps,
        bool stop
    )
    {
        IReadOnlyList<MediaBackupPhaseStep> phaseSteps = steps
            .Select(step => new MediaBackupPhaseStep
            {
                StepId = GetPipelineStepId(step),
                Order = step.Order,
                TimerName = step.TimerName,
                SkipWhenStopped = step.SkipWhenStopped,
            })
            .ToList();

        return _phaseOrchestrationService.BuildExecutionPlan(phaseSteps, stop);
    }

    private static string GetPipelineStepId(IMediaBackupPipelineStep step) =>
        step.GetType().FullName ?? step.GetType().Name;
}
