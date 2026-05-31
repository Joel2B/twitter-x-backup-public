using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupPipelineStepCompositionService
{
    IReadOnlyList<MediaBackupPhaseStep> BuildPhaseSteps(
        IEnumerable<MediaBackupPipelineStepDescriptorInput> steps
    );
}
