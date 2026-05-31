using Backup.Infrastructure.Logging;
using Backup.Application.Media.Backup.Models;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Utility.Abstractions.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Backup.Infrastructure.Media.Services;

public partial class MediaBackup
{
    private async Task CheckDuplicates()
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

            IReadOnlyList<string> memory = _mediaBackupPathProjectionService.ToArchivePaths(
                kvp.Value.Data.Select(item => item.Path)
            );

            MediaBackupDuplicateCheckPlan plan = _mediaBackupDuplicateCheckPlanningService.Plan(
                memory,
                storage.ToList()
            );
            MediaBackupDuplicateChunkExecutionPlan executionPlan =
                _mediaBackupDuplicateChunkOrchestrationService.BuildExecutionPlan(plan, 10);

            if (executionPlan.HasMemoryDuplicates)
            {
                _logger.LogInformation("memory duplicates");

                _logger.LogInformation(
                    "{id,-3} {paths} {duplicates}",
                    kvp.Key,
                    executionPlan.MemoryDuplicatePathCount,
                    executionPlan.MemoryDuplicateEntryCount
                );
            }

            if (executionPlan.HasStorageDuplicates)
            {
                _logger.LogInformation("storage duplicates");

                _logger.LogInformation(
                    "{id,-3} {paths} {duplicates}",
                    kvp.Key,
                    executionPlan.StorageDuplicatePathCount,
                    executionPlan.StorageDuplicateEntryCount
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

                    foreach (MediaBackupDuplicateCleanupOperation operation in executionPlan.CleanupOperations)
                        writeZip.RemoveEntry(operation.EntryPath, operation.RemoveDuplicateEntries);
                }
                finally
                {
                    _logger.LogInfo("disposing");
                    writeZip.Dispose();
                }

                _logger.LogInformation(
                    "{paths} duplicate paths removed",
                    executionPlan.RemovedDuplicatePathCount
                );
            }

            if (!executionPlan.IsConsistent)
            {
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
                    executionPlan.MissingCount,
                    executionPlan.ExtrasCount
                );

                if (executionPlan.ShouldRemoveExtras)
                {
                    _logger.LogInformation("paths extras (10):");

                    foreach (string item in executionPlan.ExtraPathsPreview)
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

                        foreach (string item in executionPlan.ExtraPathsToRemove)
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

                    _logger.LogInformation(
                        "{paths} paths extras removed in storage",
                        executionPlan.ExtraPathsToRemove.Count
                    );
                }
            }

            int removedExtras = executionPlan.ShouldRemoveExtras
                ? executionPlan.ExtraPathsToRemove.Count
                : 0;
            storageCount = _mediaBackupDuplicateChunkOrchestrationService.UpdateStorageCount(
                storageCount,
                storage.Count(),
                removedExtras
            );
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
