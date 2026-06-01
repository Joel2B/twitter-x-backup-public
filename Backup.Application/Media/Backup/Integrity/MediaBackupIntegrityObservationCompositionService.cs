using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupIntegrityObservationCompositionService
    : IMediaBackupIntegrityObservationCompositionService
{
    public IReadOnlyList<MediaBackupIntegrityObservation> BuildChunkObservations(
        int chunkId,
        IEnumerable<MediaBackupChunkEntryState> entries,
        IReadOnlyDictionary<string, long?> actualFileSizeByPath,
        IReadOnlyDictionary<string, uint?> actualCrc32ByPath
    ) =>
        entries
            .Select(entry =>
            {
                actualFileSizeByPath.TryGetValue(entry.Path, out long? actualFileSize);
                actualCrc32ByPath.TryGetValue(entry.Path, out uint? actualCrc32);

                return new MediaBackupIntegrityObservation
                {
                    ChunkId = chunkId,
                    Path = entry.Path,
                    ExpectedFileSize = entry.FileSize,
                    ActualFileSize = actualFileSize,
                    ExpectedCrc32 = entry.Crc32,
                    ActualCrc32 = actualCrc32,
                };
            })
            .ToList();

    public IReadOnlyList<MediaBackupIntegrityObservation> BuildObservations(
        IEnumerable<MediaBackupIntegrityObservationInput> inputs
    ) =>
        inputs
            .Select(input => new MediaBackupIntegrityObservation
            {
                ChunkId = input.ChunkId,
                Path = input.Path,
                ExpectedFileSize = input.ExpectedFileSize,
                ActualFileSize = input.ActualFileSize,
                ExpectedCrc32 = input.ExpectedCrc32,
                ActualCrc32 = input.ActualCrc32,
            })
            .ToList();

    public IReadOnlyList<MediaBackupChunkPathMetadataState> BuildPathMetadataStates(
        IEnumerable<MediaBackupChunkPathMetadataInput> inputs
    ) =>
        inputs
            .Select(input => new MediaBackupChunkPathMetadataState
            {
                Path = input.Path,
                FileSize = input.FileSize,
                Crc32 = input.Crc32,
            })
            .ToList();

    public IReadOnlyList<MediaBackupIntegrityPathChange> BuildPathChanges(
        IEnumerable<MediaBackupIntegrityChange> changes
    ) =>
        changes
            .Select(change => new MediaBackupIntegrityPathChange
            {
                ChunkId = change.ChunkId,
                Path = change.Path,
            })
            .ToList();
}
