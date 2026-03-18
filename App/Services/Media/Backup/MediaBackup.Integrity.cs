using Backup.App.Extensions;
using Backup.App.Interfaces.Services.Media;
using Backup.App.Interfaces.Services.UtilsService;
using Backup.App.Models.Media;
using Backup.App.Models.Media.Backup;
using Backup.App.Models.Utils;
using Microsoft.Extensions.Logging;

namespace Backup.App.Services.Media;

public partial class MediaBackup : IMediaBackup
{
    public async Task CheckIntegrity()
    {
        _logger.LogInformation("checking integrity backup");

        _changes.Clear();

        foreach (var kvp in _chunks)
        {
            if (kvp.Value.Data.Count == 0)
                continue;

            _logger.LogInformation("processing chunk {chunk}", kvp.Key);
            Stream? zipFile = await _mediaBackup.GetChunk(_chunks[kvp.Key]);

            if (zipFile is null)
            {
                _logger.LogError("error in GetChunk");
                continue;
            }

            _logger.LogInfo("read zip");
            IZipWriter zip = _zipWriterFactory.Open(zipFile);

            _logger.LogInfo("reading entries");
            Dictionary<string, ZipEntry> entries = zip.GetEntries().ToDictionary(o => o.FullName);

            _logger.LogInfo("checking changes");

            foreach (ChunkData item in kvp.Value.Data)
            {
                Cache? cache = await _mediaData.GetCache(item.Path);
                entries.TryGetValue(item.Path.Replace('\\', '/'), out ZipEntry? value2);

                IntegrityChange change = new()
                {
                    Id = kvp.Key,
                    Path = item.Path,
                    FileSize = new() { Diff1 = item.FileSize, Diff2 = cache?.Size?.File },
                    Crc32 = new() { Diff1 = item.Crc32, Diff2 = value2?.Crc32 },
                };

                if (
                    change.FileSize.Diff1 == change.FileSize.Diff2
                    && change.Crc32.Diff1 == change.Crc32.Diff2
                )
                    continue;

                _changes.Add(change);
            }

            _logger.LogInfo("disposing");
            zip.Dispose();
            zipFile.Dispose();

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

    public async Task FixIntegrity()
    {
        var changes = _changes
            .GroupBy(o => o.Id)
            .Select(o => new { Id = o.Key, Paths = o.ToList() })
            .ToDictionary(o => o.Id);

        _logger.LogInformation("processing changes");

        foreach (var change in changes)
        {
            _logger.LogInformation("processing chunk {chunk}", change.Key);
            Stream? zipFile = await _mediaBackup.GetChunk(_chunks[change.Key]);

            if (zipFile is null)
            {
                _logger.LogError("error in GetChunk");
                continue;
            }

            _logger.LogInfo("read zip");
            IZipWriter zip = _zipWriterFactory.Create(zipFile);

            _logger.LogInfo("applying fixes");

            foreach (IntegrityChange integrityChange in change.Value.Paths)
            {
                using Stream read = await _mediaData.Read(integrityChange.Path);
                string path = integrityChange.Path.Replace('\\', '/');

                zip.RemoveEntry(path);
                await zip.AddEntry(path, read);

                _logger.LogInfo("{path} processed", path);
            }

            _logger.LogInfo("disposing");
            zip.Dispose();
            zipFile.Dispose();
        }

        _logger.LogInformation("set new file sizes");

        foreach (var change in changes)
        {
            _logger.LogInformation("processing chunk {chunk}", change.Key);
            Stream? zipFile = await _mediaBackup.GetChunk(_chunks[change.Key]);

            if (zipFile is null)
            {
                _logger.LogError("error in GetChunk");
                continue;
            }

            _logger.LogInfo("read zip");
            IZipWriter zip = _zipWriterFactory.Open(zipFile);

            _logger.LogInfo("reading entries");
            Dictionary<string, ZipEntry> entries = zip.GetEntries().ToDictionary(o => o.FullName);

            _logger.LogInfo("expanding chunk");

            Dictionary<string, ChunkData> data = _chunks[change.Key]
                .Data.Where(o => change.Value.Paths.Select(o => o.Path).Contains(o.Path))
                .ToDictionary(o => o.Path);

            if (data.Values.Count != change.Value.Paths.Count)
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
            await _mediaBackup.Save([_chunks[change.Key]]);

            _logger.LogInfo("disposing");
            zip.Dispose();
            zipFile.Dispose();
        }
    }
}
