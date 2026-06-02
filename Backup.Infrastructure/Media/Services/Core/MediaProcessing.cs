using Backup.Application.Media;
using Backup.Application.Media.Models;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Models;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Medias;
using Backup.Infrastructure.Posts.Adapters;
using Backup.Infrastructure.Posts.Models.Stored;

namespace Backup.Infrastructure.Media.Services;

public class MediaProcessing(
    AppConfig config,
    IMediaDownloadProjectionService mediaDownloadProjectionService,
    IMediaDownloadModelMapper mediaDownloadModelMapper
) : IMediaProcessing
{
    private readonly AppConfig _config = config;
    private readonly IMediaDownloadProjectionService _mediaDownloadProjectionService =
        mediaDownloadProjectionService;
    private readonly IMediaDownloadModelMapper _mediaDownloadModelMapper = mediaDownloadModelMapper;

    private readonly List<Download> _all = [];
    private readonly List<Download> _filtered = [];

    public Task Process(List<MediaInput> posts, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<Backup.Domain.Posts.MediaInput> domainPosts = posts
            .Select(PostReplicationMapper.ToDomain)
            .ToList();

        MediaDownloadProjectionConfig projectionConfig = ToProjectionConfig(_config.Medias);
        MediaProcessingResult projected = _mediaDownloadProjectionService.Project(
            domainPosts,
            projectionConfig
        );

        _all.Clear();
        _all.AddRange(_mediaDownloadModelMapper.ToInfrastructure(projected.All));

        _filtered.Clear();
        _filtered.AddRange(_mediaDownloadModelMapper.ToInfrastructure(projected.Filtered));

        return Task.CompletedTask;
    }

    public List<Download> GetMedia() => _all.Select(Clone).ToList();

    public List<Download> GetFilteredMedia() => _filtered.Select(Clone).ToList();

    private static Download Clone(Download source) =>
        new() { Id = source.Id, Data = source.Data.Select(data => data.Clone()).ToList() };

    private static MediaDownloadProjectionConfig ToProjectionConfig(MediasConfig config) =>
        new()
        {
            Banner = ToRule(config.Banner),
            Profile = ToRule(config.Profile),
            Photo = ToRule(config.Photo),
            Gif = ToVariant(config.Gif),
            Video = ToVariant(config.Video),
        };

    private static MediaDownloadProjectionRuleConfig ToRule(MediaConfig config) =>
        new()
        {
            Filters = config.Filters is null ? null : [.. config.Filters],
            Types = config.Types is null ? null : [.. config.Types],
            Dimensions = config.Dimensions is null ? null : [.. config.Dimensions],
            Sizes = config.Sizes is null ? null : [.. config.Sizes],
        };

    private static MediaDownloadProjectionVariantConfig ToVariant(VideoConfig config) =>
        new()
        {
            Thumb = ToRule(config.Thumb),
            Types = config.Types is null ? null : [.. config.Types],
        };

    private static MediaDownloadProjectionVariantConfig ToVariant(GifConfig config) =>
        new()
        {
            Thumb = ToRule(config.Thumb),
            Types = config.Types is null ? null : [.. config.Types],
        };
}
