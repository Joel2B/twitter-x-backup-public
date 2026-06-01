using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public interface IMediaCacheRecheckPlanningService
{
    IReadOnlyCollection<string> SelectPathsToRecheck(
        IReadOnlyCollection<MediaCacheStoredEntry> entries
    );
}
