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
                entries = zip.GetEntries().ToDictionary(o => o.FullName);
            }
            finally
            {
                _logger.LogInfo("disposing");
                zip.Dispose();
            }

            _logger.LogInfo("checking changes");

            foreach (ChunkData item in kvp.Value.Data)
            {
                MediaCacheEntry? cache = await MediaData.GetCache(item.Path);
                entries.TryGetValue(item.Path.Replace('\\', '/'), out ZipEntry? value2);

                IntegrityChange change = new()
                {
                    Id = kvp.Key,
                    Path = item.Path,
                    FileSize = new() { Diff1 = item.FileSize, Diff2 = cache?.Size?.File },
                    Crc32 = new() { Diff1 = item.Crc32, Diff2 = value2?.Crc32 },
                };

                if (
                    !_mediaBackupIntegrityPlanningService.HasChange(
                        change.FileSize?.Diff1,
                        change.FileSize?.Diff2,
                        change.Crc32?.Diff1,
                        change.Crc32?.Diff2
                    )
                )
                    continue;

                _changes.Add(change);
            }

            _logger.LogInformation("chunk {chunk} processed", kvp.Key);
        }

        if (_changes.Count > 0)
            _logger.LogInfo(
                "{id,-3} {diff1,-10} {diff2,-10} {diff,-5} {path}",
                "id",
                "diff1",
                "diff2",
                "diff",
                "path"
            );

        foreach (IntegrityChange change in _changes)
        {
            _logger.LogInformation(
                "{id,-3} {diff1,-10} {diff2,-10} {diff,-5} {path}",
                change.Id,
                change.FileSize?.Diff1,
                change.FileSize?.Diff2,
                change.FileSize?.Diff1 - change.FileSize?.Diff2,
                change.Path
            );
        }
    }

    private async Task FixIntegrity()
    {
        IReadOnlyList<MediaBackupIntegrityChunkGroup> changes = _mediaBackupIntegrityPlanningService
            .GroupByChunk(
                _changes.Select(change => new MediaBackupIntegrityPathChange
                {
                    ChunkId = change.Id,
                    Path = change.Path,
                })
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
                    using Stream read = await MediaData.Read(path);
                    string relativePath = path.Replace('\\', '/');

                    zip.RemoveEntry(relativePath);
                    await zip.AddEntry(relativePath, read);

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
                entries = zip.GetEntries().ToDictionary(o => o.FullName);
            }
            finally
            {
                _logger.LogInfo("disposing");
                zip.Dispose();
            }

            _logger.LogInfo("expanding chunk");

            HashSet<string> changedPaths = [.. change.Paths];
            Dictionary<string, ChunkData> data = _chunks[change.ChunkId]
                .Data.Where(chunkData => changedPaths.Contains(chunkData.Path))
                .ToDictionary(o => o.Path);

            if (data.Values.Count != change.Paths.Count)
                throw new Exception();

            foreach (var item in data.Values)
            {
                entries.TryGetValue(item.Path.Replace('\\', '/'), out ZipEntry? value);

                if (value is null)
                    throw new Exception();

                item.FileSize = value.FileSize;
                item.Crc32 = value.Crc32;

                _logger.LogInfo("{path} updated", item.Path);
            }

            _logger.LogInfo("saving chunk");
            await _mediaBackupData.Save([_chunks[change.ChunkId]]);
        }
    }
}
