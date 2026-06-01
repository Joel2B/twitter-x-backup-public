using Backup.Application.Media.Backup.Models;
using Backup.Infrastructure.Logging;
using Backup.Infrastructure.Utility.Abstractions.Services;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Services;

internal sealed class MediaBackupDuplicatePhase : IMediaBackupDuplicatePhase
{
    public async Task CheckDuplicates(MediaBackupRuntime runtime)
    {
        int storageCount = 0;

        foreach (
            KeyValuePair<int, Backup.Infrastructure.Media.Models.Backup.Chunk> kvp in runtime
                .Context
                .Chunks
        )
        {
            if (kvp.Value.Data.Count == 0)
                continue;

            runtime.Logger.LogInformation("processing chunk {chunk}", kvp.Key);
            IZipWriter? zip = null;
            IEnumerable<string> storage = [];

            try
            {
                runtime.Logger.LogInfo("read zip");
                zip = await runtime.OpenChunkZipRead(kvp.Value, "check-duplicates");

                if (zip is null)
                    continue;

                runtime.Logger.LogInfo("reading entries");
                storage = [.. zip.GetEntries().Select(o => o.FullName)];
            }
            catch (Exception ex)
            {
                runtime.Logger.LogError(
                    ex,
                    "error while checking duplicates for chunk {chunk}",
                    kvp.Key
                );
            }
            finally
            {
                runtime.Logger.LogInfo("disposing");
                zip?.Dispose();
            }

            if (zip is null)
                continue;

            MediaBackupDuplicateChunkExecutionResult executionResult =
                runtime.Dependencies.DuplicateChunkExecutionService.Execute(
                    kvp.Value.Data.Select(item => item.Path),
                    storage,
                    runtime.GetDuplicateCleanupPreviewLimit()
                );
            IReadOnlyList<string> memory = executionResult.MemoryArchivePaths;
            IReadOnlyList<string> storagePaths = executionResult.StorageArchivePaths;
            MediaBackupDuplicateChunkExecutionPlan executionPlan = executionResult.ExecutionPlan;

            if (executionPlan.HasMemoryDuplicates)
            {
                runtime.Logger.LogInformation("memory duplicates");
                runtime.Logger.LogInformation(
                    "{id,-3} {paths} {duplicates}",
                    kvp.Key,
                    executionPlan.MemoryDuplicatePathCount,
                    executionPlan.MemoryDuplicateEntryCount
                );
            }

            if (executionPlan.HasStorageDuplicates)
            {
                runtime.Logger.LogInformation("storage duplicates");
                runtime.Logger.LogInformation(
                    "{id,-3} {paths} {duplicates}",
                    kvp.Key,
                    executionPlan.StorageDuplicatePathCount,
                    executionPlan.StorageDuplicateEntryCount
                );
                runtime.Logger.LogInformation("processing chunk {chunk}", kvp.Key);

                IZipWriter? writeZip = await runtime.OpenChunkZipWrite(
                    kvp.Value,
                    "check-duplicates-remove"
                );

                if (writeZip is null)
                    continue;

                try
                {
                    runtime.Logger.LogInfo("removing entries");

                    foreach (
                        MediaBackupDuplicateCleanupOperation operation in executionPlan.CleanupOperations
                    )
                        writeZip.RemoveEntry(operation.EntryPath, operation.RemoveDuplicateEntries);
                }
                finally
                {
                    runtime.Logger.LogInfo("disposing");
                    writeZip.Dispose();
                }

                runtime.Logger.LogInformation(
                    "{paths} duplicate paths removed",
                    executionPlan.RemovedDuplicatePathCount
                );
            }

            if (!executionPlan.IsConsistent)
            {
                runtime.Logger.LogInfo(
                    "{id,-3} {memory,-6} {storage,-6} {missing,-6} {extras,-6}",
                    "id",
                    "memory",
                    "storage",
                    "missing",
                    "extras"
                );

                runtime.Logger.LogInformation(
                    "{id,-3} {memory,-6} {storage,-6} {missing,-6} {extras,-6}",
                    kvp.Key,
                    memory.Count(),
                    storagePaths.Count,
                    executionPlan.MissingCount,
                    executionPlan.ExtrasCount
                );

                if (executionPlan.ShouldRemoveExtras)
                {
                    runtime.Logger.LogInformation("paths extras (10):");

                    foreach (string item in executionPlan.ExtraPathsPreview)
                        runtime.Logger.LogInfo("{path}", item);

                    runtime.Logger.LogInformation("processing chunk {chunk}", kvp.Key);

                    IZipWriter? writeZip = await runtime.OpenChunkZipWrite(
                        kvp.Value,
                        "check-duplicates-extras"
                    );

                    if (writeZip is null)
                        continue;

                    try
                    {
                        runtime.Logger.LogInformation("removing paths extras");

                        foreach (string item in executionPlan.ExtraPathsToRemove)
                        {
                            writeZip.RemoveEntry(item);
                            runtime.Logger.LogInfo("{path} removed", item);
                        }
                    }
                    finally
                    {
                        runtime.Logger.LogInfo("disposing");
                        writeZip.Dispose();
                    }

                    runtime.Logger.LogInformation(
                        "{paths} paths extras removed in storage",
                        executionPlan.ExtraPathsToRemove.Count
                    );
                }
            }

            storageCount = runtime.Dependencies.DuplicateChunkExecutionService.UpdateStorageCount(
                storageCount,
                executionResult
            );
        }

        runtime.Logger.LogInfo("{paths,-6} {memory,-6} {storage,-6}", "paths", "memory", "storage");

        runtime.Logger.LogInformation(
            "{paths,-6} {memory,-6} {storage,-6}",
            runtime.Context.Paths.Count,
            runtime.Context.Chunks.Values.Sum(o => o.Data.Count),
            storageCount
        );
    }
}
