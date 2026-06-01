using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupDirectPathSelectionService : IMediaBackupDirectPathSelectionService
{
    public MediaBackupDirectPathSelectionResult Select(
        IEnumerable<string> pathsInChunks,
        IEnumerable<string> directPathCandidates
    )
    {
        List<string> chunkPaths = pathsInChunks.ToList();
        List<string> directPaths = directPathCandidates.ToList();

        List<string> pathsInBoth = chunkPaths.Intersect(directPaths).ToList();
        List<string> filteredDirectPaths = directPaths.Except(pathsInBoth).ToList();

        return new MediaBackupDirectPathSelectionResult
        {
            PathsInBoth = pathsInBoth,
            DirectPaths = filteredDirectPaths,
        };
    }
}
