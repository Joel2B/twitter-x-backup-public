using Backup.Infrastructure.Logging;
using Backup.Infrastructure.Interfaces.Services.Media;
using Backup.Infrastructure.Interfaces.Services.UtilsService;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Backup.Infrastructure.Services.Media;

public partial class MediaBackup : IMediaBackupStrategy
{
    public async Task CheckDuplicates()
    {
        int storageCount = 0;

        foreach (var kvp in _chunks)
        {
            if (kvp.Value.Data.Count == 0)
                continue;

            _logger.LogInformation("processing chunk {chunk}", kvp.Key);
            IZipWriter? zip = null;
            IEnumerable<string> storage = [];

            try
            {
                _logger.LogInfo("read zip");
                zip = await OpenChunkZipRead(kvp.Value, "check-duplicates");

                if (zip is null)
                    continue;

                _logger.LogInfo("reading entries");
                storage = [.. zip.GetEntries().Select(o => o.FullName)];
            }
            catch (Exception ex)
            {
                _logger.LogError("Error: {error}", JsonConvert.SerializeObject(ex));
            }
            finally
            {
                _logger.LogInfo("disposing");
                zip?.Dispose();
            }

            if (zip is null)
                continue;

            IEnumerable<string> memory = [.. kvp.Value.Data.Select(o => o.Path.Replace('\\', '/'))];

            storageCount += storage.Count();

            _logger.LogInfo("reading memory for duplicates");

            var memoryDuplicates = memory
                .GroupBy(o => o)
                .Where(o => o.Count() > 1)
                .Select(o => new
                {
                    Name = o.Key,
                    Count = o.Count(),
                    Entries = o.ToList(),
                })
                .ToList();

            _logger.LogInfo("reading storage for duplicates");

            var storageDuplicates = storage
                .GroupBy(o => o)
                .Where(o => o.Count() > 1)
                .Select(o => new
                {
                    Name = o.Key,
                    Count = o.Count(),
                    Entries = o.ToList(),
                })
                .ToList();

            if (memoryDuplicates.Count != 0)
            {
                _logger.LogInformation("memory duplicates");

                _logger.LogInformation(
                    "{id,-3} {paths} {duplicates}",
                    kvp.Key,
                    memoryDuplicates.Count,
                    memoryDuplicates.Sum(o => o.Entries.Count)
                );
            }

            if (storageDuplicates.Count != 0)
            {
                _logger.LogInformation("storage duplicates");

                _logger.LogInformation(
                    "{id,-3} {paths} {duplicates}",
                    kvp.Key,
                    storageDuplicates.Count,
                    storageDuplicates.Sum(o => o.Entries.Count)
                );

                _logger.LogInformation("processing chunk {chunk}", kvp.Key);

                IZipWriter? writeZip = await OpenChunkZipWrite(
                    kvp.Value,
                    "check-duplicates-remove"
                );

                if (writeZip is null)
                    continue;

                try
                {
                    _logger.LogInfo("removing entries");

                    foreach (var item in storageDuplicates)
                        writeZip.RemoveEntry(item.Entries[0], true);
                }
                finally
                {
                    _logger.LogInfo("disposing");
                    writeZip.Dispose();
                }

                _logger.LogInformation(
                    "{paths} duplicate paths removed",
                    storageDuplicates.Sum(o => o.Entries.Count) - storageDuplicates.Count
                );
            }

            int missing = memory.Except(storage).Count();
            IEnumerable<string> extras = storage.Except(memory);

            if (missing == 0 && !extras.Any())
                continue;

            _logger.LogInfo(
                "{id,-3} {memory,-6} {storage,-6} {missing,-6} {extras,-6}",
                "id",
                "memory",
                "storage",
                "missing",
                "extras"
            );

            _logger.LogInformation(
                "{id,-3} {memory,-6} {storage,-6} {missing,-6} {extras,-6}",
                kvp.Key,
                memory.Count(),
                storage.Count(),
                missing,
                extras.Count()
            );

            if (extras.Any())
            {
                _logger.LogInformation("paths extras (10):");

                foreach (var item in extras.Take(10))
                    _logger.LogInfo("{path}", item);

                _logger.LogInformation("processing chunk {chunk}", kvp.Key);

                IZipWriter? writeZip = await OpenChunkZipWrite(
                    kvp.Value,
                    "check-duplicates-extras"
                );

                if (writeZip is null)
                    continue;

                try
                {
                    _logger.LogInformation("removing paths extras");

                    foreach (var item in extras)
                    {
                        writeZip.RemoveEntry(item);
                        _logger.LogInfo("{path} removed", item);
                    }
                }
                finally
                {
                    _logger.LogInfo("disposing");
                    writeZip.Dispose();
                }

                storageCount -= extras.Count();

                _logger.LogInformation("{paths} paths extras removed in storage", extras.Count());
            }
        }

        // algunos posts comparten los mismos medios
        // https://x.com/onlyadultq/status/1966755057040585071
        // https://x.com/onlyadultq/status/1962282772820816327
        _logger.LogInfo("{paths,-6} {memory,-6} {storage,-6}", "paths", "memory", "storage");

        _logger.LogInformation(
            "{paths,-6} {memory,-6} {storage,-6}",
            _paths.Count,
            _chunks.Values.Sum(o => o.Data.Count),
            storageCount
        );
    }
}

