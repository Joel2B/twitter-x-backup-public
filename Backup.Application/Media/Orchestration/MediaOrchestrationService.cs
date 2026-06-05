using Backup.Application.Media.Models;
using Backup.Application.Media.Ports;
using Microsoft.Extensions.Logging;

namespace Backup.Application.Media;

public sealed class MediaOrchestrationService(ILogger<MediaOrchestrationService> logger)
    : IMediaOrchestrationService
{
    private readonly ILogger<MediaOrchestrationService> _logger = logger;

    public async Task Run(
        IMediaOrchestrationCommand command,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation("media orchestration: loading media inputs");
        IReadOnlyList<Domain.Posts.MediaInput> posts = await command.GetMediaInputs(
            cancellationToken
        );

        _logger.LogInformation("media orchestration: loaded {PostCount} media inputs", posts.Count);

        if (posts.Count == 0)
        {
            _logger.LogInformation("media orchestration: no media inputs to process");
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation("media orchestration: processing media inputs");
        MediaProcessingResult result = await command.Process(posts, cancellationToken);
        List<MediaDownload> all = result.All.Select(download => download.Clone()).ToList();

        List<MediaDownload> filtered = result
            .Filtered.Select(download => download.Clone())
            .ToList();

        _logger.LogInformation(
            "media orchestration: processing completed with {AllCount} total downloads and {FilteredCount} filtered downloads",
            all.Count,
            filtered.Count
        );

        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation("media orchestration: pruning {DownloadCount} downloads", all.Count);
        await command.Prune(all, cancellationToken);

        _logger.LogInformation(
            "media orchestration: filtering {DownloadCount} downloads",
            filtered.Count
        );
        await command.Filter(filtered, cancellationToken);

        IReadOnlyList<string> storageIds = command.GetStorageIds();

        _logger.LogInformation(
            "media orchestration: evaluating {StorageCount} storages",
            storageIds.Count
        );

        foreach (string storageId in storageIds)
        {
            if (!command.HasMaintenance(storageId))
            {
                _logger.LogInformation(
                    "media orchestration: storage {StorageId} has no maintenance configured, skipping maintenance steps",
                    storageId
                );
                continue;
            }

            List<MediaDownload> filteredCloned = filtered
                .Select(download => download.Clone())
                .ToList();

            List<MediaDownload> filteredIntegrity = filtered
                .Select(download => download.Clone())
                .ToList();

            _logger.LogInformation(
                "media orchestration: starting storage pipeline for {StorageId} with {DownloadCount} downloads",
                storageId,
                filteredCloned.Count
            );

            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogInformation("media orchestration: {StorageId} prune storage", storageId);

            await command.PruneStorage(storageId, all, cancellationToken);
            _logger.LogInformation(
                "media orchestration: {StorageId} check storage data",
                storageId
            );

            await command.CheckStorageData(storageId, filteredCloned, cancellationToken);
            _logger.LogInformation(
                "media orchestration: {StorageId} check storage integrity",
                storageId
            );

            await command.CheckStorageIntegrity(storageId, filteredIntegrity, cancellationToken);
            filteredCloned.AddRange(filteredIntegrity);

            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation(
                "media orchestration: {StorageId} download to storage with {DownloadCount} downloads",
                storageId,
                filteredCloned.Count
            );

            await command.DownloadToStorage(storageId, filteredCloned, cancellationToken);
            _logger.LogInformation(
                "media orchestration: {StorageId} filter post-download set",
                storageId
            );

            await command.Filter(filteredCloned, cancellationToken);
            _logger.LogInformation(
                "media orchestration: {StorageId} re-check storage data",
                storageId
            );

            await command.CheckStorageData(storageId, filteredCloned, cancellationToken);
            _logger.LogInformation(
                "media orchestration: {StorageId} replicate from storage",
                storageId
            );

            await command.ReplicateFromStorage(storageId, filteredCloned, cancellationToken);
            _logger.LogInformation(
                "media orchestration: {StorageId} refresh filtered set",
                storageId
            );

            await command.Filter(filtered, cancellationToken);
            _logger.LogInformation(
                "media orchestration: completed storage pipeline for {StorageId}",
                storageId
            );
        }

        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            "media orchestration: running backups for {DownloadCount} downloads",
            filtered.Count
        );
        await command.RunBackups(filtered, cancellationToken);

        _logger.LogInformation("media orchestration: completed");
    }
}
