using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupChunkMetadataRefreshExecutionService(
    IMediaBackupChunkMetadataObservationCompositionService mediaBackupChunkMetadataObservationCompositionService,
    IMediaBackupChunkMetadataOrchestrationService mediaBackupChunkMetadataOrchestrationService
) : IMediaBackupChunkMetadataRefreshExecutionService
{
    private readonly IMediaBackupChunkMetadataObservationCompositionService
        _mediaBackupChunkMetadataObservationCompositionService =
            mediaBackupChunkMetadataObservationCompositionService;
    private readonly IMediaBackupChunkMetadataOrchestrationService
        _mediaBackupChunkMetadataOrchestrationService = mediaBackupChunkMetadataOrchestrationService;

    public bool RequiresRefresh(IEnumerable<MediaBackupChunkEntryState> entries) =>
        _mediaBackupChunkMetadataOrchestrationService.RequiresRefresh(
            _mediaBackupChunkMetadataObservationCompositionService.BuildPathMetadataStates(entries)
        );

    public MediaBackupChunkMetadataRefreshExecutionResult Refresh(
        IEnumerable<MediaBackupChunkEntryState> entries,
        IReadOnlyDictionary<string, MediaBackupChunkDataMetadata> archiveMetadataByPath
    )
    {
        IReadOnlyList<MediaBackupChunkEntryState> entryList = entries.ToList();

        IReadOnlyList<MediaBackupChunkMetadataObservationInput> observationInputs = entryList
            .Select(entry =>
            {
                archiveMetadataByPath.TryGetValue(entry.Path, out MediaBackupChunkDataMetadata? metadata);

                return new MediaBackupChunkMetadataObservationInput
                {
                    Path = entry.Path,
                    HasEntry = metadata is not null,
                    CurrentFileSize = entry.FileSize,
                    CurrentCrc32 = entry.Crc32,
                    EntryFileSize = metadata?.FileSize,
                    EntryCrc32 = metadata?.Crc32,
                };
            })
            .ToList();

        IReadOnlyList<MediaBackupChunkMetadataObservation> observations =
            _mediaBackupChunkMetadataObservationCompositionService.BuildObservations(
                observationInputs
            );

        IReadOnlyDictionary<string, MediaBackupChunkDataMetadata> updates =
            _mediaBackupChunkMetadataOrchestrationService.PlanUpdates(observations);

        int updatedCount = 0;

        IReadOnlyList<MediaBackupChunkEntryState> updatedEntries = entryList
            .Select(entry =>
            {
                if (!updates.TryGetValue(entry.Path, out MediaBackupChunkDataMetadata? metadata))
                    return entry;

                bool hasChange =
                    entry.FileSize != metadata.FileSize ||
                    entry.Crc32 != metadata.Crc32;

                if (hasChange)
                    updatedCount++;

                return new MediaBackupChunkEntryState
                {
                    Path = entry.Path,
                    Hash = entry.Hash,
                    FileSize = metadata.FileSize,
                    Crc32 = metadata.Crc32,
                };
            })
            .ToList();

        return new MediaBackupChunkMetadataRefreshExecutionResult
        {
            Entries = updatedEntries,
            UpdatedCount = updatedCount,
        };
    }
}
