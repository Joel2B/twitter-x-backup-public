using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupPhaseOrchestrationService : IMediaBackupPhaseOrchestrationService
{
    public IReadOnlyList<MediaBackupPhaseExecutionStep> BuildExecutionPlan(
        IEnumerable<MediaBackupPhaseStep> steps,
        bool shouldStop
    )
    {
        List<MediaBackupPhaseExecutionStep> plan = [];

        foreach (
            MediaBackupPhaseStep step in steps
                .OrderBy(item => item.Order)
                .ThenBy(item => item.TimerName, StringComparer.Ordinal)
                .ThenBy(item => item.StepId, StringComparer.Ordinal)
        )
        {
            if (shouldStop && step.SkipWhenStopped)
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
}
