using Backup.Application.Media.Filter;
using Backup.Application.Media.Models;
using Backup.Domain.Posts;

namespace Backup.Application.Media;

public sealed class MediaDownloadProjectionService(
    IMediaDownloadFilterPolicyService downloadFilterPolicyService,
    IMediaDownloadDataBuilderService mediaDownloadDataBuilderService,
    IMediaVideoVariantPolicyService mediaVideoVariantPolicyService,
    IMediaDuplicateFilterService mediaDuplicateFilterService
) : IMediaDownloadProjectionService
{
    private readonly IMediaDownloadFilterPolicyService _downloadFilterPolicyService =
        downloadFilterPolicyService;
    private readonly IMediaDownloadDataBuilderService _mediaDownloadDataBuilderService =
        mediaDownloadDataBuilderService;
    private readonly IMediaVideoVariantPolicyService _mediaVideoVariantPolicyService =
        mediaVideoVariantPolicyService;
    private readonly IMediaDuplicateFilterService _mediaDuplicateFilterService =
        mediaDuplicateFilterService;

    public MediaProcessingResult Project(
        IReadOnlyList<MediaInput> posts,
        MediaDownloadProjectionConfig config
    )
    {
        Dictionary<string, MediaDownload> all = [];
        Dictionary<string, MediaDownload> filtered = [];

        ProcessPhoto(posts, config.Photo, all, filtered);
        ApplyDedup(all, filtered);

        ProcessGif(posts, config.Gif, all, filtered);
        ApplyDedup(all, filtered);

        ProcessProfile(posts, config.Profile, all, filtered);
        ApplyDedup(all, filtered);

        ProcessBanner(posts, config.Banner, all, filtered);
        ApplyDedup(all, filtered);

        ProcessVideo(posts, config.Video, all, filtered);
        ApplyDedup(all, filtered);

        return new MediaProcessingResult { All = [.. all.Values], Filtered = [.. filtered.Values] };
    }

    private void ProcessPhoto(
        IReadOnlyList<MediaInput> posts,
        MediaDownloadProjectionRuleConfig config,
        Dictionary<string, MediaDownload> all,
        Dictionary<string, MediaDownload> filtered
    )
    {
        RuleProjectionContext? context = CreateRuleProjectionContext(config);

        if (context is null)
            return;

        foreach ((MediaInput post, PostMedia media) in EnumerateTypedMedias(posts, "photo"))
        {
            string id = GetFileIdWithoutExtension(media.Url);

            AddRuleBasedDownloads(
                resultId: post.Id,
                postId: post.Id,
                mediaType: "photo",
                id: media.Id,
                midPath: [id],
                sourceUrl: media.Url,
                context,
                all,
                filtered
            );
        }
    }

    private void ProcessGif(
        IReadOnlyList<MediaInput> posts,
        MediaDownloadProjectionVariantConfig config,
        Dictionary<string, MediaDownload> all,
        Dictionary<string, MediaDownload> filtered
    )
    {
        RuleProjectionContext? context = CreateRuleProjectionContext(config.Thumb);

        if (context is null || config.Types is null)
            return;

        foreach ((MediaInput post, PostMedia media) in EnumerateTypedMedias(posts, "animated_gif"))
        {
            if (media.VideoInfo?.Variants is null)
                continue;

            string id = GetFileIdWithoutExtension(media.Url);

            AddRuleBasedDownloads(
                resultId: post.Id,
                postId: post.Id,
                mediaType: "gif",
                id: media.Id,
                midPath: ["thumb", id],
                sourceUrl: media.Url,
                context,
                all,
                filtered
            );

            AddGifVariantDownloads(post, media, config.Types, all, filtered);
        }
    }

    private void ProcessVideo(
        IReadOnlyList<MediaInput> posts,
        MediaDownloadProjectionVariantConfig config,
        Dictionary<string, MediaDownload> all,
        Dictionary<string, MediaDownload> filtered
    )
    {
        RuleProjectionContext? context = CreateRuleProjectionContext(config.Thumb);

        if (context is null || config.Types is null)
            return;

        foreach ((MediaInput post, PostMedia media) in EnumerateTypedMedias(posts, "video"))
        {
            if (media.VideoInfo?.Variants is null)
                continue;

            string id = GetFileIdWithoutExtension(media.Url);

            AddRuleBasedDownloads(
                resultId: post.Id,
                postId: post.Id,
                mediaType: "video",
                id: media.Id,
                midPath: ["thumb", id],
                sourceUrl: media.Url,
                context,
                all,
                filtered
            );

            AddVideoVariantDownloads(post, media, config.Types, all, filtered);
        }
    }

    private void ProcessProfile(
        IReadOnlyList<MediaInput> posts,
        MediaDownloadProjectionRuleConfig config,
        Dictionary<string, MediaDownload> all,
        Dictionary<string, MediaDownload> filtered
    )
    {
        IReadOnlyList<Resolution>? resolutions = CreateResolutions(config);

        if (resolutions is null)
            return;

        foreach (MediaInput post in posts)
        {
            string? url = post.Profile.ImageUrl;

            if (string.IsNullOrWhiteSpace(url))
                continue;

            string? fileName = Path.GetFileName(url);

            if (string.IsNullOrWhiteSpace(fileName))
                continue;

            string extension = Path.GetExtension(fileName).Trim('.');
            string id = fileName.Split("_normal")[0];

            foreach (Resolution resolution in resolutions)
            {
                MediaDownloadData built = _mediaDownloadDataBuilderService.Build(
                    new()
                    {
                        PostId = "profiles",
                        MediaType = post.Profile.Id,
                        Id = "profile",
                        MidPath = [id],
                        FormatType = extension,
                        ResolutionType = resolution.Type,
                        Name = resolution.Name,
                        Url = url.Replace("normal", resolution.Name),
                        IncludeQuery = false,
                    }
                );

                AddDataDownload(post.Id, built, include: true, all, filtered);
            }
        }
    }

    private void ProcessBanner(
        IReadOnlyList<MediaInput> posts,
        MediaDownloadProjectionRuleConfig config,
        Dictionary<string, MediaDownload> all,
        Dictionary<string, MediaDownload> filtered
    )
    {
        IReadOnlyList<Resolution>? resolutions = CreateResolutions(config);

        if (resolutions is null)
            return;

        foreach (MediaInput post in posts)
        {
            string? url = post.Profile.BannerUrl;

            if (string.IsNullOrWhiteSpace(url))
                continue;

            string id = Path.GetFileName(url);

            foreach (Resolution resolution in resolutions)
            {
                MediaDownloadData built = _mediaDownloadDataBuilderService.Build(
                    new()
                    {
                        PostId = "profiles",
                        MediaType = post.Profile.Id,
                        Id = "banner",
                        MidPath = [id],
                        FormatType = "jpg",
                        ResolutionType = resolution.Type,
                        Name = resolution.Name,
                        Url = $"{url}/{resolution.Name}",
                        IncludeQuery = false,
                    }
                );

                AddDataDownload(post.Id, built, include: true, all, filtered);
            }
        }
    }

    private void AddRuleBasedDownloads(
        string resultId,
        string postId,
        string mediaType,
        string id,
        IReadOnlyList<string> midPath,
        string sourceUrl,
        RuleProjectionContext context,
        Dictionary<string, MediaDownload> all,
        Dictionary<string, MediaDownload> filtered
    )
    {
        string extension = Path.GetExtension(sourceUrl).Trim('.');

        foreach (string type in context.Types)
        {
            foreach (Resolution resolution in context.Resolutions)
            {
                MediaDownloadData built = _mediaDownloadDataBuilderService.Build(
                    new()
                    {
                        PostId = postId,
                        MediaType = mediaType,
                        Id = id,
                        MidPath = [.. midPath],
                        FormatType = type,
                        ResolutionType = resolution.Type,
                        Name = resolution.Name,
                        Url = sourceUrl,
                    }
                );

                bool include = !_downloadFilterPolicyService.IsExcluded(
                    context.Filters,
                    extension,
                    type,
                    resolution.Name
                );

                AddDataDownload(resultId, built, include, all, filtered);
            }
        }
    }

    private void AddGifVariantDownloads(
        MediaInput post,
        PostMedia media,
        IReadOnlyCollection<string> allowedTypes,
        Dictionary<string, MediaDownload> all,
        Dictionary<string, MediaDownload> filtered
    )
    {
        foreach (PostVariant variant in media.VideoInfo?.Variants ?? [])
        {
            if (!allowedTypes.Contains(variant.ContentType))
                continue;

            string url = StripQuery(variant.Url);
            string videoId = GetFileIdWithoutExtension(url);
            MediaDownloadData built = _mediaDownloadDataBuilderService.Build(
                new()
                {
                    PostId = post.Id,
                    MediaType = "gif",
                    Id = media.Id,
                    FormatType = "mp4",
                    ResolutionType = videoId,
                    Name = "index",
                    Url = url,
                    IncludeQuery = false,
                }
            );

            AddDataDownload(post.Id, built, include: true, all, filtered);
        }
    }

    private void AddVideoVariantDownloads(
        MediaInput post,
        PostMedia media,
        IReadOnlyCollection<string> allowedTypes,
        Dictionary<string, MediaDownload> all,
        Dictionary<string, MediaDownload> filtered
    )
    {
        foreach (PostVariant variant in media.VideoInfo?.Variants ?? [])
        {
            if (!allowedTypes.Contains(variant.ContentType))
                continue;

            string? formatType = _mediaVideoVariantPolicyService.GetFormatType(variant.ContentType);

            if (formatType is null)
                continue;

            string url = StripQuery(variant.Url);
            string videoId = GetFileIdWithoutExtension(url);
            string? resolution = _mediaVideoVariantPolicyService.GetResolution(formatType, url);

            if (resolution is null)
            {
                throw new InvalidOperationException(
                    $"Unable to resolve media video resolution from variant URL '{url}'."
                );
            }

            MediaDownloadData built = _mediaDownloadDataBuilderService.Build(
                new()
                {
                    PostId = post.Id,
                    MediaType = "video",
                    Id = media.Id,
                    FormatType = formatType,
                    ResolutionType = videoId,
                    Name = resolution,
                    Url = url,
                    IncludeQuery = false,
                }
            );

            AddDataDownload(post.Id, built, include: true, all, filtered);
        }
    }

    private void ApplyDedup(
        Dictionary<string, MediaDownload> all,
        Dictionary<string, MediaDownload> filtered
    )
    {
        ApplyDedup(all);
        ApplyDedup(filtered);
    }

    private void ApplyDedup(Dictionary<string, MediaDownload> downloads)
    {
        IReadOnlyList<MediaDownload> deduped = _mediaDuplicateFilterService.Filter(
            [.. downloads.Values]
        );

        downloads.Clear();

        foreach (MediaDownload download in deduped)
            downloads[download.Id] = download.Clone();
    }

    private RuleProjectionContext? CreateRuleProjectionContext(
        MediaDownloadProjectionRuleConfig config
    )
    {
        if (config.Types is null)
            return null;

        IReadOnlyList<Resolution>? resolutions = CreateResolutions(config);

        if (resolutions is null)
            return null;

        return new RuleProjectionContext(
            _downloadFilterPolicyService.Parse(config.Filters),
            resolutions,
            config.Types
        );
    }

    private static IReadOnlyList<Resolution>? CreateResolutions(
        MediaDownloadProjectionRuleConfig config
    )
    {
        if (config.Dimensions is null || config.Sizes is null)
            return null;

        return
        [
            .. config.Dimensions.Select(value => new Resolution(value, "dimension")),
            .. config.Sizes.Select(value => new Resolution(value, "size")),
        ];
    }

    private static IEnumerable<(MediaInput Post, PostMedia Media)> EnumerateTypedMedias(
        IReadOnlyList<MediaInput> posts,
        string type
    )
    {
        foreach (MediaInput post in posts)
        {
            foreach (PostMedia media in post.Medias ?? [])
            {
                if (media.Type == type)
                    yield return (post, media);
            }
        }
    }

    private static string GetFileIdWithoutExtension(string url) =>
        Path.GetFileNameWithoutExtension(Path.GetFileName(StripQuery(url)));

    private static string StripQuery(string url) => url.Split('?')[0];

    private static void AddDataDownload(
        string id,
        MediaDownloadData data,
        bool include,
        Dictionary<string, MediaDownload> all,
        Dictionary<string, MediaDownload> filtered
    )
    {
        MediaDownload allDownload = GetOrCreate(all, id);
        allDownload.Data.Add(data.Clone());

        if (!include)
            return;

        MediaDownload filteredDownload = GetOrCreate(filtered, id);
        filteredDownload.Data.Add(data.Clone());
    }

    private static MediaDownload GetOrCreate(Dictionary<string, MediaDownload> downloads, string id)
    {
        if (downloads.TryGetValue(id, out MediaDownload? existing))
            return existing;

        MediaDownload created = new() { Id = id, Data = [] };
        downloads[id] = created;
        return created;
    }

    private sealed record Resolution(string Name, string Type);

    private sealed record RuleProjectionContext(
        IReadOnlyList<MediaExclusionRule> Filters,
        IReadOnlyList<Resolution> Resolutions,
        IReadOnlyList<string> Types
    );
}
