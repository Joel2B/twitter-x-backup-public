using Backup.Infrastructure.Logging;
using Backup.Application.Media.Backup.Models;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Utility.Abstractions.Services;
using Backup.Infrastructure.Media.Models;
using Backup.Infrastructure.Media.Models.Backup;
using Backup.Infrastructure.Models.Utils;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Services;

public partial class MediaBackup
{
    private async Task CheckIntegrity()
    {
        _logger.LogInformation("checking integrity backup");

        _changes.Clear();
        List<MediaBackupIntegrityObservation> observations = [];

        foreach (var kvp in _chunks)
        {
            if (kvp.Value.Data.Count == 0)
                continue;

            _logger.LogInformation("processing chunk {chunk}", kvp.Key);
            IZipWriter? zip = await OpenChunkZipRead(_chunks[kvp.Key], "check-integrity");

            if (zip is null)
                continue;

            Dictionary<string, ZipEntry> entries;

            try
            {
                _logger.LogInfo("read zip");
                _logger.LogInfo("reading entries");
                entries = _mediaBackupZipEntryReaderIoService.ReadEntriesByFullName(zip);
            }
            finally
            {
                _logger.LogInfo("disposing");
                zip.Dispose();
            }

            _logger.LogInfo("checking changes");
            IReadOnlyList<MediaBackupChunkEntryState> entryStates = BuildChunkEntryStates(
                kvp.Value.Data
            );
            Dictionary<string, long?> actualFileSizeByPath = new(StringComparer.Ordinal);
            Dictionary<string, uint?> actualCrc32ByPath = new(StringComparer.Ordinal);

            foreach (ChunkData item in kvp.Value.Data)
            {
                MediaCacheEntry? cache = await MediaData.GetCache(item.Path);
                entries.TryGetValue(
                    _mediaBackupPathProjectionService.ToArchivePath(item.Path),
                    out ZipEntry? value2
                );

                actualFileSizeByPath[item.Path] = cache?.Size?.File;
                actualCrc32ByPath[item.Path] = value2?.Crc32;
            }

            observations.AddRange(
                _mediaBackupIntegrityObservationCompositionService.BuildChunkObservations(
                    kvp.Key,
                    entryStates,
                    actualFileSizeByPath,
                    actualCrc32ByPath
                )
            );

            _logger.LogInformation("chunk {chunk} processed", kvp.Key);
        }

        _changes.AddRange(_mediaBackupIntegrityChangeDetectionService.Detect(observations));

        if (_changes.Count > 0)
            _logger.LogInfo(
                "{id,-3} {diff1,-10} {diff2,-10} {diff,-5} {path}",
                "id",
                "diff1",
                "diff2",
                "diff",
                "path"
            );

        foreach (MediaBackupIntegrityChange change in _changes)
        {
            _logger.LogInformation(
                "{id,-3} {diff1,-10} {diff2,-10} {diff,-5} {path}",
                change.ChunkId,
                change.ExpectedFileSize,
                change.ActualFileSize,
                change.ExpectedFileSize - change.ActualFileSize,
                change.Path
            );
        }
    }

    private async Task FixIntegrity()
    {
        IReadOnlyList<MediaBackupIntegrityChunkGroup> changes = _mediaBackupIntegrityPlanningService
            .GroupByChunk(
                _mediaBackupIntegrityObservationCompositionService.BuildPathChanges(_changes)
            );

        _logger.LogInformation("processing changes");

        foreach (MediaBackupIntegrityChunkGroup change in changes)
        {
            _logger.LogInformation("processing chunk {chunk}", change.ChunkId);
            IZipWriter? zip = await OpenChunkZipWrite(_chunks[change.ChunkId], "fix-integrity");

            if (zip is null)
                continue;

            try
            {
                _logger.LogInfo("applying fixes");

                foreach (string path in change.Paths)
                {
                    string relativePath = _mediaBackupPathProjectionService.ToArchivePath(path);
                    await _mediaBackupZipMutationIoService.ReplaceEntryFromMediaStorage(
                        MediaData,
                        zip,
                        path,
                        relativePath
                    );

                    _logger.LogInfo("{path} processed", relativePath);
                }
            }
            finally
            {
                _logger.LogInfo("disposing");
                zip.Dispose();
            }
        }

        _logger.LogInformation("set new file sizes");

        foreach (MediaBackupIntegrityChunkGroup change in changes)
        {
            _logger.LogInformation("processing chunk {chunk}", change.ChunkId);

            IZipWriter? zip = await OpenChunkZipRead(
                _chunks[change.ChunkId],
                "set-new-file-sizes-after-fix"
            );

            if (zip is null)
                continue;

            Dictionary<string, ZipEntry> entries;

            try
            {
                _logger.LogInfo("reading entries");
                entries = _mediaBackupZipEntryReaderIoService.ReadEntriesByFullName(zip);
            }
            finally
            {
                _logger.LogInfo("disposing");
                zip.Dispose();
            }

            _logger.LogInfo("expanding chunk");
            IReadOnlyDictionary<string, MediaBackupChunkDataMetadata> metadataByArchivePath =
                entries.ToDictionary(
                    item => item.Key,
                    item => new MediaBackupChunkDataMetadata
                    {
                        FileSize = item.Value.FileSize,
                        Crc32 = item.Value.Crc32,
                    },
                    StringComparer.Ordinal
                );

            IReadOnlyDictionary<string, MediaBackupChunkDataMetadata> metadataByPath =
                _mediaBackupPathArchiveMetadataProjectionService.BuildPathMetadataByPath(
                    change.Paths,
                    metadataByArchivePath
                );

            MediaBackupIntegrityChunkApplyResult applyResult =
                _mediaBackupIntegrityChunkRefreshService.Refresh(
                    change.Paths,
                    _chunks[change.ChunkId].Data.Select(chunkData => chunkData.Path),
                    metadataByPath,
                    BuildChunkEntryStates(_chunks[change.ChunkId].Data)
                );

            ApplyChunkEntryStates(_chunks[change.ChunkId], applyResult.Entries);

            foreach (string path in applyResult.UpdatedPaths)
                _logger.LogInfo("{path} updated", path);

            _logger.LogInfo("saving chunk");
            await _mediaBackupChunkPersistenceIoService.SaveChunk(
                _mediaBackupData,
                _chunks[change.ChunkId]
            );
        }
    }
}
