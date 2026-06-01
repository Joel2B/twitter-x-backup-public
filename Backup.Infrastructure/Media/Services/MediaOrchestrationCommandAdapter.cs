using Backup.Application.Media.Models;
using Backup.Application.Media.Ports;
using Backup.Application.Media;
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
    IMediaProcessing mediaProcessing,
    IMediaPrune mediaPrune,
    IEnumerable<IMediaStorage> mediaData,
    IEnumerable<IMediaDataMaintenance> mediaMaintenance,
    IMediaIntegrity mediaIntegrity,
    IMediaFilter mediaFilter,
    IMediaReplication mediaReplication,
    IEnumerable<IMediaBackupStrategy> mediaBackups,
    IMediaDownloadService mediaDownload,
    IMediaDownloadModelMapper mediaDownloadModelMapper
) : IMediaOrchestrationCommand
{
    private readonly ILogger<MediaOrchestrationCommandAdapter> _logger = logger;
    private readonly IPostDomainData _postData = postData;
    private readonly IMediaOrchestrationStorageResolutionService _mediaOrchestrationStorageResolutionService =
        mediaOrchestrationStorageResolutionService;
    private readonly IMediaProcessing _mediaProcessing = mediaProcessing;
    private readonly IMediaPrune _mediaPrune = mediaPrune;
    private readonly Dictionary<string, IMediaStorage> _mediaData = mediaData
        .Where(item => !string.IsNullOrWhiteSpace(item.Id))
        .ToDictionary(item => item.Id!, item => item, StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IMediaDataMaintenance> _mediaMaintenance = mediaMaintenance
        .Where(item => item.Id is not null)
        .ToDictionary(item => item.Id!, item => item, StringComparer.OrdinalIgnoreCase);
    private readonly IMediaIntegrity _mediaIntegrity = mediaIntegrity;
    private readonly IMediaFilter _mediaFilter = mediaFilter;
    private readonly IMediaReplication _mediaReplication = mediaReplication;
    private readonly List<IMediaBackupStrategy> _mediaBackups = mediaBackups.ToList();
    private readonly IMediaDownloadService _mediaDownload = mediaDownload;
    private readonly IMediaDownloadModelMapper _mediaDownloadModelMapper = mediaDownloadModelMapper;

    public async Task<IReadOnlyList<Backup.Domain.Posts.MediaInput>> GetMediaInputs() =>
        await _postData.GetMediaInputs() ?? [];

    public async Task<MediaProcessingResult> Process(IReadOnlyList<Backup.Domain.Posts.MediaInput> posts)
    {
        List<Backup.Infrastructure.Posts.Models.MediaInput> appPosts = posts
            .Select(PostReplicationMapper.ToApp)
            .ToList();

        await _mediaProcessing.Process(appPosts);

        return new()
        {
            All = _mediaDownloadModelMapper.ToApplication(_mediaProcessing.GetMedia()),
            Filtered = _mediaDownloadModelMapper.ToApplication(_mediaProcessing.GetFilteredMedia()),
        };
    }

    public async Task Prune(List<MediaDownload> downloads)
    {
        await ExecuteOnInfrastructureDownloads(downloads, _mediaPrune.Prune);
    }

    public async Task Filter(List<MediaDownload> downloads)
    {
        await ExecuteOnInfrastructureDownloads(downloads, _mediaFilter.Check);
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
            _logger.LogWarning("no media maintenance configured for media data {storageId}", storageId);

        return has;
    }

    public async Task PruneStorage(string storageId, List<MediaDownload> downloads)
    {
        IMediaDataMaintenance? maintenance = GetMaintenance(storageId);

        if (maintenance is null)
            return;

        await ExecuteOnInfrastructureDownloads(downloads, maintenance.Prune);
    }

    public async Task CheckStorageData(string storageId, List<MediaDownload> downloads)
    {
        IMediaDataMaintenance? maintenance = GetMaintenance(storageId);

        if (maintenance is null)
            return;

        await ExecuteOnInfrastructureDownloads(downloads, maintenance.CheckData);
    }

    public async Task CheckStorageIntegrity(string storageId, List<MediaDownload> downloads)
    {
        IMediaDataMaintenance? maintenance = GetMaintenance(storageId);

        if (maintenance is null)
            return;

        await ExecuteOnInfrastructureDownloads(downloads, async infra =>
        {
            await _mediaIntegrity.Check(infra, maintenance);
        });
    }

    public async Task DownloadToStorage(string storageId, List<MediaDownload> downloads)
    {
        IMediaStorage? storage = GetStorage(storageId);

        if (storage is null)
            return;

        await ExecuteOnInfrastructureDownloads(downloads, async infra =>
        {
            await _mediaDownload.Download(infra, storage);
        });
    }

    public async Task ReplicateFromStorage(string storageId, List<MediaDownload> downloads)
    {
        IMediaStorage? storage = GetStorage(storageId);

        if (storage is null)
            return;

        await ExecuteOnInfrastructureDownloads(downloads, async infra =>
        {
            await _mediaReplication.Replicate(infra, _mediaData.Values, storage);
        });
    }

    public async Task RunBackups(List<MediaDownload> downloads)
    {
        string? backupSourceId = _mediaOrchestrationStorageResolutionService.SelectBackupSourceId(
            _mediaData.Keys
        );
        IMediaStorage? backupSource = backupSourceId is null ? null : _mediaData[backupSourceId];

        if (backupSource is null)
        {
            foreach (IMediaBackupStrategy backup in _mediaBackups)
                _logger.LogWarning("no media data configured for backup source for backup {backupId}", backup.Id);

            return;
        }

        List<Download> infra = _mediaDownloadModelMapper.ToInfrastructure(downloads);

        foreach (IMediaBackupStrategy backup in _mediaBackups)
            await backup.Backup(infra, backupSource);
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
        Func<List<Download>, Task> action
    )
    {
        List<Download> infra = _mediaDownloadModelMapper.ToInfrastructure(downloads);
        await action(infra);
        Sync(downloads, infra);
    }
}
