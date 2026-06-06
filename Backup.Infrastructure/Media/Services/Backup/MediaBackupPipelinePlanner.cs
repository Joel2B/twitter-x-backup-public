using Backup.Application.Media.Backup.Models;
using Backup.Infrastructure.Media.Abstractions.Services;

namespace Backup.Infrastructure.Media.Services;

internal sealed class MediaBackupPipelinePlanner
{
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

        List<MediaBackupPhaseExecutionStep> plan = [];

        foreach (
            MediaBackupPhaseStep step in phaseSteps
                .OrderBy(item => item.Order)
                .ThenBy(item => item.TimerName, StringComparer.Ordinal)
                .ThenBy(item => item.StepId, StringComparer.Ordinal)
        )
        {
            if (stop && step.SkipWhenStopped)
                break;

            plan.Add(
                new MediaBackupPhaseExecutionStep
                {
                    StepId = step.StepId,
                    TimerName = step.TimerName,
                }
            );
        }

        return plan;
    }

    private static string GetPipelineStepId(IMediaBackupPipelineStep step) =>
        step.GetType().FullName ?? step.GetType().Name;
}
