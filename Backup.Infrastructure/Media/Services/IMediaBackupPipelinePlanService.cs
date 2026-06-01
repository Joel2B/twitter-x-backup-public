using Backup.Application.Media.Backup.Models;
using Backup.Infrastructure.Media.Abstractions.Services;

namespace Backup.Infrastructure.Media.Services;

internal interface IMediaBackupPipelinePlanService
{
    IReadOnlyList<MediaBackupPhaseExecutionStep> BuildExecutionPlan(
        IEnumerable<IMediaBackupPipelineStep> steps,
        bool stop
    );
}
