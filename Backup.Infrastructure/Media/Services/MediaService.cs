using Backup.Infrastructure.Logging;
using Backup.Infrastructure.Interfaces.Data.Posts;
using Backup.Infrastructure.Interfaces.Services.Media;
using Backup.Infrastructure.Models.Media;
using Backup.Infrastructure.Posts.Adapters;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Services.Media;

public class MediaService(
    ILogger<MediaService> _logger,
    IPostDomainData _postData,
    IMediaProcessing _mediaProcessing,
    IMediaPrune _mediaPrune,
    IEnumerable<IMediaStorage> _mediaData,
    IEnumerable<IMediaDataMaintenance> _mediaMaintenance,
    IMediaIntegrity _mediaIntegrity,
    IMediaFilter _mediaFilter,
    IMediaReplication _mediaReplication,
    IEnumerable<IMediaBackupStrategy> _mediaBackup,
    IMediaDownloadService _mediaDownload
) : IMediaService
{
    private readonly ILogger<MediaService> _logger = _logger;
    private readonly IPostDomainData _postData = _postData;
    private readonly IMediaProcessing _mediaProcessing = _mediaProcessing;
    private readonly IMediaPrune _mediaPrune = _mediaPrune;
    private readonly IEnumerable<IMediaStorage> _mediaData = _mediaData;
    private readonly Dictionary<string, IMediaDataMaintenance> _mediaMaintenance = _mediaMaintenance
        .Where(m => m.Id is not null)
        .ToDictionary(m => m.Id!, m => m, StringComparer.OrdinalIgnoreCase);
    private readonly IMediaIntegrity _mediaIntegrity = _mediaIntegrity;
    private readonly IMediaFilter _mediaFilter = _mediaFilter;
    private readonly IMediaReplication _mediaReplication = _mediaReplication;
    private readonly IEnumerable<IMediaBackupStrategy> _mediaBackups = _mediaBackup;
    private readonly IMediaDownloadService _mediaDownload = _mediaDownload;

    public async Task Download()
    {
        List<Backup.Domain.Posts.MediaInput>? posts;

        using (_logger.LogTimer("getting posts"))
            posts = await _postData.GetMediaInputs();

        if (posts is null)
            return;

        List<Backup.Infrastructure.Models.Posts.MediaInput> appPosts = posts
            .Select(PostReplicationMapper.ToApp)
            .ToList();

        _logger.LogInformation("processing {posts} posts", appPosts.Count);

        await _mediaProcessing.Process(appPosts);

        List<Download> all = _mediaProcessing.GetMedia();
        List<Download> filtered = _mediaProcessing.GetFilteredMedia();

        int media = GetCount(all);
        int filteredMedia = GetCount(filtered);

        _logger.LogInformation("{media} processed media", media);
        _logger.LogInformation("{media} excluded media", media - filteredMedia);
        _logger.LogInformation("{media} filtered media", filteredMedia);

        using (_logger.LogTimer("filtering media for pruning"))
            await _mediaPrune.Prune(all);

        _logger.LogInformation("filtering media");
        await _mediaFilter.Check(filtered);

        foreach (IMediaStorage data in _mediaData)
        {
            if (data.Id is null || !_mediaMaintenance.TryGetValue(data.Id, out IMediaDataMaintenance? maintenance))
            {
                _logger.LogWarning(data.Id, "no media maintenance configured for media data");
                continue;
            }

            List<Download> filteredCloned = [.. filtered.Select(dl => dl.Clone())];
            List<Download> filteredIntegrity = [.. filtered.Select(dl => dl.Clone())];

            using (_logger.LogTimer(data.Id, $"pruning {GetCount(all)} media"))
                await maintenance.Prune(all);

            using (_logger.LogTimer(data.Id, "checking saved media"))
                await maintenance.CheckData(filteredCloned);

            using (_logger.LogTimer(data.Id, "checking integrity media"))
            {
                await _mediaIntegrity.Check(filteredIntegrity, maintenance);
                _logger.LogInformation(data.Id, "{media} corrupt media", filteredIntegrity.Count);

                filteredCloned.AddRange(filteredIntegrity);
            }

            _logger.LogInformation(
                data.Id,
                "downloading {posts} posts, {media} media",
                filteredCloned.Count,
                GetCount(filteredCloned)
            );

            await _mediaDownload.Download(filteredCloned, data);

            _logger.LogInformation(data.Id, "filtering media (local)");
            await _mediaFilter.Check(filteredCloned);

            using (_logger.LogTimer(data.Id, "checking saved media"))
                await maintenance.CheckData(filteredCloned);

            _logger.LogInformation(data.Id, "replicating media");
            await _mediaReplication.Replicate(filteredCloned, _mediaData, data);

            _logger.LogInformation(data.Id, "filtering media (global)");
            await _mediaFilter.Check(filtered);
        }

        foreach (IMediaBackupStrategy backup in _mediaBackups)
        {
            IMediaStorage? backupSource = _mediaData.FirstOrDefault();

            if (backupSource is null)
            {
                _logger.LogWarning(backup.Id, "no media data configured for backup source");
                continue;
            }

            using (_logger.LogTimer(backup.Id, "backing up media"))
                await backup.Backup(filtered, backupSource);
        }
    }

    private static int GetCount(List<Download> downloads) =>
        downloads.SelectMany(o => o.Data).Count();
}

