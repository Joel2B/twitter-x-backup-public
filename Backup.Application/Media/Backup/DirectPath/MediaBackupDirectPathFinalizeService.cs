using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupDirectPathFinalizeService(
    IMediaBackupDirectPathQueueService directPathQueueService,
    IMediaBackupDirectPathSelectionService directPathSelectionService
) : IMediaBackupDirectPathFinalizeService
{
    private readonly IMediaBackupDirectPathQueueService _directPathQueueService =
        directPathQueueService;
    private readonly IMediaBackupDirectPathSelectionService _directPathSelectionService =
        directPathSelectionService;

    public MediaBackupDirectPathFinalizeResult Finalize(
        IEnumerable<string> pathsInChunks,
        IEnumerable<string> directPaths
    )
    {
        IReadOnlyList<string> normalizedDirectPaths = _directPathQueueService.Normalize(
            directPaths
        );

        MediaBackupDirectPathSelectionResult selection = _directPathSelectionService.Select(
            pathsInChunks,
            normalizedDirectPaths
        );

        return new MediaBackupDirectPathFinalizeResult
        {
            PathsInBoth = selection.PathsInBoth.ToList(),
            DirectPaths = _directPathQueueService.Normalize(selection.DirectPaths).ToList(),
        };
    }
}
