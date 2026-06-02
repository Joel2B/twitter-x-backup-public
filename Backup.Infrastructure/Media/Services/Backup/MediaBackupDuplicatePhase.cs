using Backup.Application.Media.Backup;
using Backup.Application.Media.Backup.Models;
using Backup.Infrastructure.Logging;
using Backup.Infrastructure.Models.Utils;
using Backup.Infrastructure.Utility.Abstractions.Services;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Services;

internal sealed class MediaBackupDuplicatePhase(
    IMediaBackupDuplicateChunkExecutionService duplicateChunkExecutionService
) : IMediaBackupDuplicatePhase
{
    private readonly IMediaBackupDuplicateChunkExecutionService _duplicateChunkExecutionService =
        duplicateChunkExecutionService;

    public async Task CheckDuplicates(
        MediaBackupRuntime runtime,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        int storageCount = 0;

        foreach (
            KeyValuePair<int, Backup.Infrastructure.Media.Models.Backup.Chunk> kvp in runtime
                .Context
                .Chunks
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (kvp.Value.Data.Count == 0)
                continue;

            runtime.Logger.LogInformation("processing chunk {chunk}", kvp.Key);
            Dictionary<string, ZipEntry>? entries = null;
            IEnumerable<string> storage = [];

            try
            {
                entries = await runtime.ReadChunkEntries(kvp.Value, "check-duplicates");

                if (entries is null)
                    continue;

                storage = entries.Keys;
            }
            catch (Exception ex)
            {
                runtime.Logger.LogError(
                    ex,
                    "error while checking duplicates for chunk {chunk}",
                    kvp.Key
                );
            }
            if (entries is null)
                continue;

            MediaBackupDuplicateChunkExecutionResult executionResult =
                _duplicateChunkExecutionService.Execute(
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

                bool removed = await runtime.MutateChunkZip(
                    kvp.Value,
                    "check-duplicates-remove",
                    zip =>
                    {
                        runtime.Logger.LogInfo("removing entries");

                        foreach (
                            MediaBackupDuplicateCleanupOperation operation in executionPlan.CleanupOperations
                        )
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            zip.RemoveEntry(
                                operation.EntryPath,
                                operation.RemoveDuplicateEntries
                            );
                        }

                        return Task.CompletedTask;
                    }
                );

                if (!removed)
                    continue;

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

                    bool removed = await runtime.MutateChunkZip(
                        kvp.Value,
                        "check-duplicates-extras",
                        zip =>
                        {
                            runtime.Logger.LogInformation("removing paths extras");

                            foreach (string item in executionPlan.ExtraPathsToRemove)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                zip.RemoveEntry(item);
                                runtime.Logger.LogInfo("{path} removed", item);
                            }

                            return Task.CompletedTask;
                        }
                    );

                    if (!removed)
                        continue;

                    runtime.Logger.LogInformation(
                        "{paths} paths extras removed in storage",
                        executionPlan.ExtraPathsToRemove.Count
                    );
                }
            }

            storageCount = _duplicateChunkExecutionService.UpdateStorageCount(
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
