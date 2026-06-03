using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupIntegrityChunkUpdateOrchestrationService(
    IMediaBackupIntegrityChunkDataSelectionService mediaBackupIntegrityChunkDataSelectionService
) : IMediaBackupIntegrityChunkUpdateOrchestrationService
{
    private readonly IMediaBackupIntegrityChunkDataSelectionService _mediaBackupIntegrityChunkDataSelectionService =
        mediaBackupIntegrityChunkDataSelectionService;

    public MediaBackupIntegrityUpdateSelectionPlan SelectAndValidate(
        IEnumerable<string> changedPaths,
        IEnumerable<string> chunkPaths,
        IReadOnlyDictionary<string, MediaBackupChunkDataMetadata> metadataByPath
    )
    {
        MediaBackupIntegrityChunkDataSelectionResult selected =
            _mediaBackupIntegrityChunkDataSelectionService.Select(changedPaths, chunkPaths);

        if (!selected.IsComplete)
        {
            throw new InvalidOperationException(
                $"missing paths while fixing integrity: {string.Join(", ", selected.MissingPaths)}"
            );
        }

        List<string> selectedPaths = selected.SelectedPaths.ToList();
        Dictionary<string, MediaBackupChunkDataMetadata> selectedMetadata = [];

        foreach (string path in selectedPaths)
        {
            if (!metadataByPath.TryGetValue(path, out MediaBackupChunkDataMetadata? metadata))
                throw new KeyNotFoundException($"missing zip metadata while fixing integrity: {path}");

            selectedMetadata[path] = metadata;
        }

        return new MediaBackupIntegrityUpdateSelectionPlan
        {
            SelectedPaths = selectedPaths,
            PathMetadata = selectedMetadata,
        };
    }
}
