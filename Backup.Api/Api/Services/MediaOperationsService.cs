using Backup.Api.Errors;
using Backup.Api.Models;
using Backup.Application.Media.Models;
using Backup.Application.Media.Ports;
using Backup.Infrastructure.Media.Abstractions.Services;

namespace Backup.Api.Services;

public sealed class MediaOperationsService(
    Backup.Infrastructure.Media.Abstractions.Services.IMediaService mediaService,
    IMediaOrchestrationCommand mediaOrchestrationCommand,
    IEnumerable<IMediaStorage> mediaStorage,
    IEnumerable<IMediaDataMaintenance> mediaMaintenance
)
{
    private readonly Backup.Infrastructure.Media.Abstractions.Services.IMediaService _mediaService =
        mediaService;
    private readonly IMediaOrchestrationCommand _mediaOrchestrationCommand =
        mediaOrchestrationCommand;
    private readonly IReadOnlyDictionary<string, IMediaStorage> _storageById = mediaStorage
        .Where(storage => !string.IsNullOrWhiteSpace(storage.Id))
        .ToDictionary(storage => storage.Id!, storage => storage, StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _maintenanceIds = mediaMaintenance
        .Where(maintenance => !string.IsNullOrWhiteSpace(maintenance.Id))
        .Select(maintenance => maintenance.Id!)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public async Task<OperationResult> Run(CancellationToken cancellationToken)
    {
        await _mediaService.Download(cancellationToken);
        return new OperationResult("media-run", "completed");
    }

    public IReadOnlyList<MediaStorageSummary> GetStorages() =>
        _storageById
            .Keys.OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .Select(id => new MediaStorageSummary
            {
                StorageId = id,
                HasMaintenance = _maintenanceIds.Contains(id),
            })
            .ToList();

    public async Task<OperationResult> RunStoragePipeline(
        string storageId,
        CancellationToken cancellationToken
    )
    {
        EnsureStorageExists(storageId);

        if (!HasMaintenance(storageId))
        {
            return new OperationResult(
                "media-storage-pipeline",
                "skipped",
                $"storage={storageId}, maintenance is not configured"
            );
        }

        PreparedMediaDownloads prepared = await PrepareDownloads(cancellationToken);
        List<MediaDownload> filteredCloned = prepared
            .Filtered.Select(download => download.Clone())
            .ToList();
        List<MediaDownload> filteredIntegrity = prepared
            .Filtered.Select(download => download.Clone())
            .ToList();

        await _mediaOrchestrationCommand.PruneStorage(storageId, prepared.All, cancellationToken);
        await _mediaOrchestrationCommand.CheckStorageData(
            storageId,
            filteredCloned,
            cancellationToken
        );
        await _mediaOrchestrationCommand.CheckStorageIntegrity(
            storageId,
            filteredIntegrity,
            cancellationToken
        );

        filteredCloned.AddRange(filteredIntegrity);

        await _mediaOrchestrationCommand.DownloadToStorage(
            storageId,
            filteredCloned,
            cancellationToken
        );
        await _mediaOrchestrationCommand.Filter(filteredCloned, cancellationToken);
        await _mediaOrchestrationCommand.CheckStorageData(
            storageId,
            filteredCloned,
            cancellationToken
        );
        await _mediaOrchestrationCommand.ReplicateFromStorage(
            storageId,
            filteredCloned,
            cancellationToken
        );
        await _mediaOrchestrationCommand.Filter(prepared.Filtered, cancellationToken);

        return new OperationResult(
            "media-storage-pipeline",
            "completed",
            $"storage={storageId}, downloads={filteredCloned.Count}"
        );
    }

    public async Task<OperationResult> PruneStorage(
        string storageId,
        CancellationToken cancellationToken
    )
    {
        EnsureStorageExists(storageId);

        if (!HasMaintenance(storageId))
        {
            return new OperationResult(
                "media-storage-prune",
                "skipped",
                $"storage={storageId}, maintenance is not configured"
            );
        }

        PreparedMediaDownloads prepared = await PrepareDownloads(cancellationToken);
        await _mediaOrchestrationCommand.PruneStorage(storageId, prepared.All, cancellationToken);

        return new OperationResult(
            "media-storage-prune",
            "completed",
            $"storage={storageId}, downloads={prepared.All.Count}"
        );
    }

    public async Task<OperationResult> CheckStorageData(
        string storageId,
        CancellationToken cancellationToken
    )
    {
        EnsureStorageExists(storageId);

        if (!HasMaintenance(storageId))
        {
            return new OperationResult(
                "media-storage-check-data",
                "skipped",
                $"storage={storageId}, maintenance is not configured"
            );
        }

        PreparedMediaDownloads prepared = await PrepareDownloads(cancellationToken);
        List<MediaDownload> downloads = prepared
            .Filtered.Select(download => download.Clone())
            .ToList();

        await _mediaOrchestrationCommand.CheckStorageData(storageId, downloads, cancellationToken);

        return new OperationResult(
            "media-storage-check-data",
            "completed",
            $"storage={storageId}, downloads={downloads.Count}"
        );
    }

    public async Task<OperationResult> CheckStorageIntegrity(
        string storageId,
        CancellationToken cancellationToken
    )
    {
        EnsureStorageExists(storageId);

        if (!HasMaintenance(storageId))
        {
            return new OperationResult(
                "media-storage-check-integrity",
                "skipped",
                $"storage={storageId}, maintenance is not configured"
            );
        }

        PreparedMediaDownloads prepared = await PrepareDownloads(cancellationToken);
        List<MediaDownload> downloads = prepared
            .Filtered.Select(download => download.Clone())
            .ToList();

        await _mediaOrchestrationCommand.CheckStorageIntegrity(
            storageId,
            downloads,
            cancellationToken
        );

        return new OperationResult(
            "media-storage-check-integrity",
            "completed",
            $"storage={storageId}, downloads={downloads.Count}"
        );
    }

    public async Task<OperationResult> DownloadToStorage(
        string storageId,
        CancellationToken cancellationToken
    )
    {
        EnsureStorageExists(storageId);
        PreparedMediaDownloads prepared = await PrepareDownloads(cancellationToken);
        List<MediaDownload> downloads = prepared
            .Filtered.Select(download => download.Clone())
            .ToList();

        await _mediaOrchestrationCommand.DownloadToStorage(storageId, downloads, cancellationToken);

        return new OperationResult(
            "media-storage-download",
            "completed",
            $"storage={storageId}, downloads={downloads.Count}"
        );
    }

    public async Task<OperationResult> ReplicateFromStorage(
        string storageId,
        CancellationToken cancellationToken
    )
    {
        EnsureStorageExists(storageId);
        PreparedMediaDownloads prepared = await PrepareDownloads(cancellationToken);
        List<MediaDownload> downloads = prepared
            .Filtered.Select(download => download.Clone())
            .ToList();

        await _mediaOrchestrationCommand.ReplicateFromStorage(
            storageId,
            downloads,
            cancellationToken
        );

        return new OperationResult(
            "media-storage-replicate",
            "completed",
            $"storage={storageId}, downloads={downloads.Count}"
        );
    }

    public async Task<OperationResult> RunBackups(CancellationToken cancellationToken)
    {
        PreparedMediaDownloads prepared = await PrepareDownloads(cancellationToken);
        await _mediaOrchestrationCommand.RunBackups(prepared.Filtered, cancellationToken);

        return new OperationResult(
            "media-backups-run",
            "completed",
            $"downloads={prepared.Filtered.Count}"
        );
    }

    private async Task<PreparedMediaDownloads> PrepareDownloads(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<Backup.Domain.Posts.MediaInput> posts =
            await _mediaOrchestrationCommand.GetMediaInputs(cancellationToken);
        MediaProcessingResult result = await _mediaOrchestrationCommand.Process(
            posts,
            cancellationToken
        );
        List<MediaDownload> all = result.All.Select(download => download.Clone()).ToList();
        List<MediaDownload> filtered = result
            .Filtered.Select(download => download.Clone())
            .ToList();

        await _mediaOrchestrationCommand.Prune(all, cancellationToken);
        await _mediaOrchestrationCommand.Filter(filtered, cancellationToken);

        return new PreparedMediaDownloads(all, filtered);
    }

    private void EnsureStorageExists(string storageId)
    {
        if (_storageById.ContainsKey(storageId))
            return;

        throw new ApiException($"media storage '{storageId}' was not found.");
    }

    private bool HasMaintenance(string storageId) => _maintenanceIds.Contains(storageId);

    private sealed record PreparedMediaDownloads(
        List<MediaDownload> All,
        List<MediaDownload> Filtered
    );
}
