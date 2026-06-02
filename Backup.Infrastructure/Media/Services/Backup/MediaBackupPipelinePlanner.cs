using Backup.Application.Media.Backup;
using Backup.Application.Media.Backup.Models;
using Backup.Infrastructure.Media.Abstractions.Services;

namespace Backup.Infrastructure.Media.Services;

internal sealed class MediaBackupPipelinePlanner(
    IMediaBackupPhaseOrchestrationService phaseOrchestrationService,
    IMediaBackupPipelineStepCompositionService pipelineStepCompositionService
) : IMediaBackupPipelinePlanService
{
    private readonly IMediaBackupPhaseOrchestrationService _phaseOrchestrationService =
        phaseOrchestrationService;
    private readonly IMediaBackupPipelineStepCompositionService _pipelineStepCompositionService =
        pipelineStepCompositionService;

    public IReadOnlyList<MediaBackupPhaseExecutionStep> BuildExecutionPlan(
        IEnumerable<IMediaBackupPipelineStep> steps,
        bool stop
    )
    {
        IReadOnlyList<MediaBackupPhaseStep> phaseSteps =
            _pipelineStepCompositionService.BuildPhaseSteps(
                steps.Select(step => new MediaBackupPipelineStepDescriptorInput
                {
                    StepId = GetPipelineStepId(step),
                    Order = step.Order,
                    TimerName = step.TimerName,
                    SkipWhenStopped = step.SkipWhenStopped,
                })
            );

        return _phaseOrchestrationService.BuildExecutionPlan(phaseSteps, stop);
    }

    private static string GetPipelineStepId(IMediaBackupPipelineStep step) =>
        step.GetType().FullName ?? step.GetType().Name;
}
