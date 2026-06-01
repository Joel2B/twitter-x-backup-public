using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public interface IMediaCacheRecheckMutationApplySelectionService
{
    MediaCacheRecheckMutationApplySelection Select(
        MediaCacheRecheckMutationApplyPlan plan,
        IReadOnlySet<string> existingPaths
    );
}
