using Backup.App.Extensions;
using Backup.App.Interfaces.Data.Post;
using Backup.App.Interfaces.Services.Media;
using Backup.App.Models.Media;
using Microsoft.Extensions.Logging;

namespace Backup.App.Services.Media;

public class MediaService(
    ILogger<MediaService> _logger,
    IEnumerable<IPostData> _postData,
    IMediaProcessing _mediaProcessing,
    IMediaPrune _mediaPrune,
    IEnumerable<IMediaData> _mediaData,
    IMediaIntegrity _mediaIntegrity,
    IMediaFilter _mediaFilter,
    IMediaReplication _mediaReplication,
    IEnumerable<IMediaBackup> _mediaBackup,
    IMediaDownload _mediaDownload
) : IMediaService
{
    private readonly ILogger<MediaService> _logger = _logger;
    private readonly IEnumerable<IPostData> _data = _postData;
    private readonly IMediaProcessing _mediaProcessing = _mediaProcessing;
    private readonly IMediaPrune _mediaPrune = _mediaPrune;
    private readonly IEnumerable<IMediaData> _mediaData = _mediaData;
    private readonly IMediaIntegrity _mediaIntegrity = _mediaIntegrity;
    private readonly IMediaFilter _mediaFilter = _mediaFilter;
    private readonly IMediaReplication _mediaReplication = _mediaReplication;
    private readonly IEnumerable<IMediaBackup> _mediaBackups = _mediaBackup;
    private readonly IMediaDownload _mediaDownload = _mediaDownload;

    public async Task Download()
    {
        List<Models.Post.Post>? posts;

        using (_logger.LogTimer("getting posts"))
            posts = await _data.First().GetAll();

        if (posts is null)
            return;

        _logger.LogInformation("processing {posts} posts", posts.Count);

        List<Models.Post.Data> changes = posts
            .SelectMany(o => o.Changes)
            .Where(o => o.Data is not null)
            .Select(o => o.Data?.Clone())
            .Cast<Models.Post.Data>()
            .ToList();

        List<Models.Post.Post> posts1 = changes
            .Select(o => new Models.Post.Post()
            {
                Id = o.Id,
                Profile = o.Profile,
                Description = o.Description,
                Retweeted = o.Retweeted,
                Favorited = o.Favorited,
                Bookmarked = o.Bookmarked,
                CreatedAt = o.CreatedAt,
                Hashtags = o.Hashtags,
                Medias = o.Medias,
                Deleted = o.Deleted,
            })
            .ToList();

        posts.AddRange(posts1);

        await _mediaProcessing.Process(posts);

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

        foreach (IMediaData data in _mediaData)
        {
            List<Download> filteredCloned = [.. filtered.Select(dl => dl.Clone())];
            List<Download> filteredIntegrity = [.. filtered.Select(dl => dl.Clone())];

            using (_logger.LogTimer(data.Id, $"pruning {GetCount(all)} media"))
                await data.Prune(all);

            using (_logger.LogTimer(data.Id, "checking saved media"))
                await data.CheckData(filteredCloned);

            using (_logger.LogTimer(data.Id, "checking integrity media"))
            {
                await _mediaIntegrity.Check(filteredIntegrity, data);
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
                await data.CheckData(filteredCloned);

            _logger.LogInformation(data.Id, "replicating media");
            await _mediaReplication.Replicate(filteredCloned, _mediaData, data);

            _logger.LogInformation(data.Id, "filtering media (global)");
            await _mediaFilter.Check(filtered);
        }

        foreach (IMediaBackup backup in _mediaBackups)
        {
            using (_logger.LogTimer(backup.Id, "backing up media"))
                await backup.Backup(filtered);
        }
    }

    private static int GetCount(List<Download> downloads) =>
        downloads.SelectMany(o => o.Data).Count();
}
