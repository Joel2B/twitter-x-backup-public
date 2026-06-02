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
    IMediaDownloadModelMapper mediaDownloadModelMapper,
    IMediaOrchestrationStorageResolutionService mediaOrchestrationStorageResolutionService
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
        mediaOrchestrationStorageResolutionService,
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
