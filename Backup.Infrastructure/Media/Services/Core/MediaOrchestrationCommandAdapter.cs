using Backup.Application.Media;
using Backup.Application.Media.Models;
using Backup.Application.Media.Ports;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Models;
using Backup.Infrastructure.Posts.Abstractions.Data;
using Backup.Infrastructure.Posts.Adapters;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Services;

public sealed class MediaOrchestrationCommandAdapter(
    ILogger<MediaOrchestrationCommandAdapter> logger,
    IPostDomainData postData,
    IMediaProcessing mediaProcessing,
    IMediaPrune mediaPrune,
    IEnumerable<IMediaStorage> mediaStorage,
    IEnumerable<IMediaDataMaintenance> mediaMaintenance,
    MediaIntegrity mediaIntegrity,
    IMediaFilter mediaFilter,
    IMediaReplication mediaReplication,
    IEnumerable<IMediaBackupStrategy> mediaBackups,
    IMediaDownloadService mediaDownload,
    IMediaDownloadModelMapper mediaDownloadModelMapper
) : IMediaOrchestrationCommand
{
    private readonly ILogger<MediaOrchestrationCommandAdapter> _logger = logger;
    private readonly IPostDomainData _postData = postData;
    private readonly IMediaProcessing _mediaProcessing = mediaProcessing;
    private readonly IMediaPrune _mediaPrune = mediaPrune;
    private readonly MediaIntegrity _mediaIntegrity = mediaIntegrity;
    private readonly IMediaFilter _mediaFilter = mediaFilter;
    private readonly IMediaReplication _mediaReplication = mediaReplication;
    private readonly List<IMediaBackupStrategy> _mediaBackups = mediaBackups.ToList();
    private readonly IMediaDownloadService _mediaDownload = mediaDownload;
    private readonly IMediaDownloadModelMapper _mediaDownloadModelMapper = mediaDownloadModelMapper;
    private readonly MediaOrchestrationResourceCatalog _resourceCatalog = new(
        logger,
        mediaStorage,
        mediaMaintenance
    );
    private readonly MediaOrchestrationDownloadMutationRunner _downloadMutationRunner = new(
        mediaDownloadModelMapper
    );

    public async Task<IReadOnlyList<Backup.Domain.Posts.MediaInput>> GetMediaInputs(
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _postData.GetMediaInputs() ?? [];
    }

    public async Task<MediaProcessingResult> Process(
        IReadOnlyList<Backup.Domain.Posts.MediaInput> posts,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<Backup.Infrastructure.Posts.Models.Stored.MediaInput> appPosts = posts
            .Select(PostReplicationMapper.ToApp)
            .ToList();

        await _mediaProcessing.Process(appPosts, cancellationToken);

        return new()
        {
            All = _mediaDownloadModelMapper.ToApplication(_mediaProcessing.GetMedia()),
            Filtered = _mediaDownloadModelMapper.ToApplication(_mediaProcessing.GetFilteredMedia()),
        };
    }

    public async Task Prune(
        List<MediaDownload> downloads,
        CancellationToken cancellationToken = default
    )
    {
        await _downloadMutationRunner.Execute(
            downloads,
            (infra, ct) => _mediaPrune.Prune(infra, ct),
            cancellationToken
        );
    }

    public async Task Filter(
        List<MediaDownload> downloads,
        CancellationToken cancellationToken = default
    )
    {
        await _downloadMutationRunner.Execute(
            downloads,
            (infra, ct) => _mediaFilter.Check(infra, ct),
            cancellationToken
        );
    }

    public IReadOnlyList<string> GetStorageIds() => _resourceCatalog.GetStorageIds();

    public bool HasMaintenance(string storageId) => _resourceCatalog.HasMaintenance(storageId);

    public async Task PruneStorage(
        string storageId,
        List<MediaDownload> downloads,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        IMediaDataMaintenance? maintenance = _resourceCatalog.GetMaintenance(storageId);

        if (maintenance is null)
            return;

        await _downloadMutationRunner.Execute(
            downloads,
            (infra, ct) => maintenance.Prune(infra, ct),
            cancellationToken
        );
    }

    public async Task CheckStorageData(
        string storageId,
        List<MediaDownload> downloads,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        IMediaDataMaintenance? maintenance = _resourceCatalog.GetMaintenance(storageId);

        if (maintenance is null)
            return;

        await _downloadMutationRunner.Execute(
            downloads,
            (infra, ct) => maintenance.CheckData(infra, ct),
            cancellationToken
        );
    }

    public async Task CheckStorageIntegrity(
        string storageId,
        List<MediaDownload> downloads,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        IMediaDataMaintenance? maintenance = _resourceCatalog.GetMaintenance(storageId);

        if (maintenance is null)
            return;

        await _downloadMutationRunner.Execute(
            downloads,
            async (infra, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                await _mediaIntegrity.Check(infra, maintenance);
            },
            cancellationToken
        );
    }

    public async Task DownloadToStorage(
        string storageId,
        List<MediaDownload> downloads,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        IMediaStorage? storage = _resourceCatalog.GetStorage(storageId);

        if (storage is null)
            return;

        await _downloadMutationRunner.Execute(
            downloads,
            async (infra, ct) =>
            {
                await _mediaDownload.Download(infra, storage, ct);
            },
            cancellationToken
        );
    }

    public async Task ReplicateFromStorage(
        string storageId,
        List<MediaDownload> downloads,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        IMediaStorage? storage = _resourceCatalog.GetStorage(storageId);

        if (storage is null)
            return;

        await _downloadMutationRunner.Execute(
            downloads,
            async (infra, ct) =>
            {
                await _mediaReplication.Replicate(infra, _resourceCatalog.Storage, storage, ct);
            },
            cancellationToken
        );
    }

    public async Task VerifyCacheParity(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<string> storageIds = _resourceCatalog.GetStorageIds().ToList();
        List<(string StorageId, int Count)> snapshots = [];

        foreach (string storageId in storageIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IMediaDataMaintenance? maintenance = _resourceCatalog.GetMaintenance(storageId);

            if (maintenance is null)
                continue;

            int count = await maintenance.GetCacheCount(cancellationToken);
            snapshots.Add((storageId, count));

            _logger.LogInformation(
                "media cache count [{StorageId}] entries={EntryCount}",
                storageId,
                count
            );
        }

        if (snapshots.Count <= 1)
        {
            _logger.LogInformation(
                "media cache parity skipped: only {Count} cache-enabled storage",
                snapshots.Count
            );
            return;
        }

        (string StorageId, int Count) = snapshots[0];

        foreach ((string StorageId, int Count) secondary in snapshots.Skip(1))
        {
            if (secondary.Count == Count)
            {
                _logger.LogInformation(
                    "media cache parity OK: primary={Primary} secondary={Secondary} count={Count}",
                    StorageId,
                    secondary.StorageId,
                    Count
                );
                continue;
            }

            _logger.LogWarning(
                "media cache parity MISMATCH: primary={Primary} secondary={Secondary} primaryCount={PrimaryCount} secondaryCount={SecondaryCount}",
                StorageId,
                secondary.StorageId,
                Count,
                secondary.Count
            );
        }
    }

    public async Task RunBackups(
        List<MediaDownload> downloads,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        IMediaStorage? backupSource = _resourceCatalog.GetBackupSource();

        if (backupSource is null)
        {
            foreach (IMediaBackupStrategy backup in _mediaBackups)
                _logger.LogWarning(
                    "no media data configured for backup source for backup {backupId}",
                    backup.Id
                );

            return;
        }

        List<Download> infra = _mediaDownloadModelMapper.ToInfrastructure(downloads);

        foreach (IMediaBackupStrategy backup in _mediaBackups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await backup.Backup(infra, backupSource, cancellationToken);
        }
    }
}
