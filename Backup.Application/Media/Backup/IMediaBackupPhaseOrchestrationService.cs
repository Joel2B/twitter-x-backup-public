using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupPhaseOrchestrationService
{
    IReadOnlyList<MediaBackupPhaseExecutionStep> BuildExecutionPlan(
        IEnumerable<MediaBackupPhaseStep> steps,
        bool shouldStop
    );
}
