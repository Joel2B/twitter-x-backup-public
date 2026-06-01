using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public interface IMediaCacheRecheckMutationExecutionService
{
    MediaCacheRecheckMutationApplySelection Execute(
        IReadOnlyList<MediaCacheRecheckMutation> mutations,
        IReadOnlySet<string> existingPaths
    );
}
