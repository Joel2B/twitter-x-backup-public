using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public interface IMediaCacheRecheckExecutionInputService
{
    MediaCacheRecheckExecutionInput BuildInputs(
        IReadOnlyList<MediaCacheStoredEntry> entries
    );
}
