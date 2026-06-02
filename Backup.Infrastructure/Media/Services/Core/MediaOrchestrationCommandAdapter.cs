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
    IMediaOrchestrationStorageResolutionService mediaOrchestrationStorageResolutionService,
    MediaOrchestrationCommandDependencies dependencies
) : IMediaOrchestrationCommand
{
    private readonly ILogger<MediaOrchestrationCommandAdapter> _logger = logger;
    private readonly IPostDomainData _postData = postData;
    private readonly IMediaOrchestrationStorageResolutionService _mediaOrchestrationStorageResolutionService =
        mediaOrchestrationStorageResolutionService;
    private readonly IMediaProcessing _mediaProcessing = dependencies.MediaProcessing;
    private readonly IMediaPrune _mediaPrune = dependencies.MediaPrune;
    private readonly Dictionary<string, IMediaStorage> _mediaData = dependencies
        .MediaStorage
        .Where(item => !string.IsNullOrWhiteSpace(item.Id))
        .ToDictionary(item => item.Id!, item => item, StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IMediaDataMaintenance> _mediaMaintenance = dependencies
        .MediaMaintenance
        .Where(item => item.Id is not null)
        .ToDictionary(item => item.Id!, item => item, StringComparer.OrdinalIgnoreCase);
    private readonly IMediaIntegrity _mediaIntegrity = dependencies.MediaIntegrity;
    private readonly IMediaFilter _mediaFilter = dependencies.MediaFilter;
    private readonly IMediaReplication _mediaReplication = dependencies.MediaReplication;
    private readonly List<IMediaBackupStrategy> _mediaBackups = dependencies
        .MediaBackups
        .ToList();
    private readonly IMediaDownloadService _mediaDownload = dependencies.MediaDownload;
    private readonly IMediaDownloadModelMapper _mediaDownloadModelMapper =
        dependencies.MediaDownloadModelMapper;

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
        await ExecuteOnInfrastructureDownloads(
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
        await ExecuteOnInfrastructureDownloads(
            downloads,
            (infra, ct) => _mediaFilter.Check(infra, ct),
            cancellationToken
        );
    }

    public IReadOnlyList<string> GetStorageIds() =>
        _mediaOrchestrationStorageResolutionService.GetStorageIds(_mediaData.Keys);

    public bool HasMaintenance(string storageId)
    {
        bool has = _mediaOrchestrationStorageResolutionService.HasMaintenance(
            storageId,
            _mediaMaintenance.Keys
        );

        if (!has)
            _logger.LogWarning(
                "no media maintenance configured for media data {storageId}",
                storageId
            );

        return has;
    }

    public async Task PruneStorage(
        string storageId,
        List<MediaDownload> downloads,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        IMediaDataMaintenance? maintenance = GetMaintenance(storageId);

        if (maintenance is null)
            return;

        await ExecuteOnInfrastructureDownloads(
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
        IMediaDataMaintenance? maintenance = GetMaintenance(storageId);

        if (maintenance is null)
            return;

        await ExecuteOnInfrastructureDownloads(
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
        IMediaDataMaintenance? maintenance = GetMaintenance(storageId);

        if (maintenance is null)
            return;

        await ExecuteOnInfrastructureDownloads(
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
        IMediaStorage? storage = GetStorage(storageId);

        if (storage is null)
            return;

        await ExecuteOnInfrastructureDownloads(
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
        IMediaStorage? storage = GetStorage(storageId);

        if (storage is null)
            return;

        await ExecuteOnInfrastructureDownloads(
            downloads,
            async (infra, ct) =>
            {
                await _mediaReplication.Replicate(infra, _mediaData.Values, storage, ct);
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
        string? backupSourceId = _mediaOrchestrationStorageResolutionService.SelectBackupSourceId(
            _mediaData.Keys
        );
        IMediaStorage? backupSource = backupSourceId is null ? null : _mediaData[backupSourceId];

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

    private IMediaStorage? GetStorage(string storageId)
    {
        string? resolvedId = _mediaOrchestrationStorageResolutionService.ResolveStorageId(
            storageId,
            _mediaData.Keys
        );

        if (resolvedId is null)
        {
            _logger.LogWarning("media storage not found: {storageId}", storageId);
            return null;
        }

        return _mediaData[resolvedId];
    }

    private IMediaDataMaintenance? GetMaintenance(string storageId)
    {
        IMediaDataMaintenance? maintenance;

        if (_mediaMaintenance.TryGetValue(storageId, out maintenance))
            return maintenance;

        _logger.LogWarning("media maintenance not found: {storageId}", storageId);
        return null;
    }

    private void Sync(List<MediaDownload> target, List<Download> source)
    {
        target.Clear();
        target.AddRange(_mediaDownloadModelMapper.ToApplication(source));
    }

    private async Task ExecuteOnInfrastructureDownloads(
        List<MediaDownload> downloads,
        Func<List<Download>, CancellationToken, Task> action,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<Download> infra = _mediaDownloadModelMapper.ToInfrastructure(downloads);
        await action(infra, cancellationToken);
        Sync(downloads, infra);
    }
}
