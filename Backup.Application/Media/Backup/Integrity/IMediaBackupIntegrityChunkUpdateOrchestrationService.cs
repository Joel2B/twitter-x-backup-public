using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupIntegrityChunkUpdateOrchestrationService
{
    MediaBackupIntegrityUpdateSelectionPlan SelectAndValidate(
        IEnumerable<string> changedPaths,
        IEnumerable<string> chunkPaths,
        IReadOnlyDictionary<string, MediaBackupChunkDataMetadata> metadataByPath
    );
}
