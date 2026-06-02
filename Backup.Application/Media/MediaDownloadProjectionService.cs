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
        ApplyDedup(all);
        ApplyDedup(filtered);

        ProcessGif(posts, config.Gif, all, filtered);
        ApplyDedup(all);
        ApplyDedup(filtered);

        ProcessProfile(posts, config.Profile, all, filtered);
        ApplyDedup(all);
        ApplyDedup(filtered);

        ProcessBanner(posts, config.Banner, all, filtered);
        ApplyDedup(all);
        ApplyDedup(filtered);

        ProcessVideo(posts, config.Video, all, filtered);
        ApplyDedup(all);
        ApplyDedup(filtered);

        return new MediaProcessingResult { All = [.. all.Values], Filtered = [.. filtered.Values] };
    }

    private void ProcessPhoto(
        IReadOnlyList<MediaInput> posts,
        MediaDownloadProjectionRuleConfig config,
        Dictionary<string, MediaDownload> all,
        Dictionary<string, MediaDownload> filtered
    )
    {
        if (config.Types is null || config.Dimensions is null || config.Sizes is null)
            return;

        IReadOnlyList<MediaExclusionRule> filters = _downloadFilterPolicyService.Parse(
            config.Filters
        );
        List<Resolution> resolutions = BuildResolutions(config.Dimensions, config.Sizes);

        foreach (MediaInput post in posts)
        {
            IEnumerable<PostMedia> medias =
                post.Medias?.Where(media => media.Type == "photo") ?? [];

            foreach (PostMedia media in medias)
            {
                string id = Path.GetFileNameWithoutExtension(media.Url);

                foreach (string type in config.Types)
                {
                    foreach (Resolution resolution in resolutions)
                    {
                        MediaDownloadData built = _mediaDownloadDataBuilderService.Build(
                            new()
                            {
                                PostId = post.Id,
                                MediaType = "photo",
                                Id = media.Id,
                                MidPath = [id],
                                FormatType = type,
                                ResolutionType = resolution.Type,
                                Name = resolution.Name,
                                Url = media.Url,
                            }
                        );

                        bool include = !_downloadFilterPolicyService.IsExcluded(
                            filters,
                            Path.GetExtension(media.Url).Trim('.'),
                            type,
                            resolution.Name
                        );

                        AddDataDownload(post.Id, built, include, all, filtered);
                    }
                }
            }
        }
    }

    private void ProcessGif(
        IReadOnlyList<MediaInput> posts,
        MediaDownloadProjectionVariantConfig config,
        Dictionary<string, MediaDownload> all,
        Dictionary<string, MediaDownload> filtered
    )
    {
        if (
            config.Thumb.Types is null
            || config.Thumb.Dimensions is null
            || config.Thumb.Sizes is null
            || config.Types is null
        )
            return;

        IReadOnlyList<MediaExclusionRule> filters = _downloadFilterPolicyService.Parse(
            config.Thumb.Filters
        );
        List<Resolution> resolutions = BuildResolutions(
            config.Thumb.Dimensions,
            config.Thumb.Sizes
        );

        IEnumerable<MediaInput> gifPosts = posts.Where(post =>
            post.Medias is not null && post.Medias.Any(media => media.Type == "animated_gif")
        );

        foreach (MediaInput post in gifPosts)
        {
            IEnumerable<PostMedia> medias = post.Medias ?? [];

            foreach (PostMedia media in medias)
            {
                if (media.Type != "animated_gif")
                    continue;

                if (media.VideoInfo?.Variants is null)
                    continue;

                string id = Path.GetFileNameWithoutExtension(media.Url);

                foreach (string type in config.Thumb.Types)
                {
                    foreach (Resolution resolution in resolutions)
                    {
                        MediaDownloadData built = _mediaDownloadDataBuilderService.Build(
                            new()
                            {
                                PostId = post.Id,
                                MediaType = "gif",
                                Id = media.Id,
                                MidPath = ["thumb", id],
                                FormatType = type,
                                ResolutionType = resolution.Type,
                                Name = resolution.Name,
                                Url = media.Url,
                            }
                        );

                        bool include = !_downloadFilterPolicyService.IsExcluded(
                            filters,
                            Path.GetExtension(media.Url).Trim('.'),
                            type,
                            resolution.Name
                        );

                        AddDataDownload(post.Id, built, include, all, filtered);
                    }
                }

                foreach (PostVariant variant in media.VideoInfo.Variants)
                {
                    if (!config.Types.Contains(variant.ContentType))
                        continue;

                    string url = variant.Url.Split('?')[0];
                    string videoFileName = Path.GetFileName(url);
                    string videoId = Path.GetFileNameWithoutExtension(videoFileName);

                    MediaDownloadData builtVideo = _mediaDownloadDataBuilderService.Build(
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

                    AddDataDownload(post.Id, builtVideo, include: true, all, filtered);
                }
            }
        }
    }

    private void ProcessVideo(
        IReadOnlyList<MediaInput> posts,
        MediaDownloadProjectionVariantConfig config,
        Dictionary<string, MediaDownload> all,
        Dictionary<string, MediaDownload> filtered
    )
    {
        if (
            config.Thumb.Types is null
            || config.Thumb.Dimensions is null
            || config.Thumb.Sizes is null
            || config.Types is null
        )
            return;

        IReadOnlyList<MediaExclusionRule> filters = _downloadFilterPolicyService.Parse(
            config.Thumb.Filters
        );
        List<Resolution> resolutions = BuildResolutions(
            config.Thumb.Dimensions,
            config.Thumb.Sizes
        );

        IEnumerable<MediaInput> videoPosts = posts.Where(post =>
            post.Medias is not null && post.Medias.Any(media => media.Type == "video")
        );

        foreach (MediaInput post in videoPosts)
        {
            IEnumerable<PostMedia> medias = post.Medias ?? [];

            foreach (PostMedia media in medias)
            {
                if (media.Type != "video" || media.VideoInfo?.Variants is null)
                    continue;

                string id = Path.GetFileNameWithoutExtension(Path.GetFileName(media.Url));

                foreach (string type in config.Thumb.Types)
                {
                    foreach (Resolution resolution in resolutions)
                    {
                        MediaDownloadData built = _mediaDownloadDataBuilderService.Build(
                            new()
                            {
                                PostId = post.Id,
                                MediaType = "video",
                                Id = media.Id,
                                MidPath = ["thumb", id],
                                FormatType = type,
                                ResolutionType = resolution.Type,
                                Name = resolution.Name,
                                Url = media.Url,
                            }
                        );

                        bool include = !_downloadFilterPolicyService.IsExcluded(
                            filters,
                            Path.GetExtension(media.Url).Trim('.'),
                            type,
                            resolution.Name
                        );

                        AddDataDownload(post.Id, built, include, all, filtered);
                    }
                }

                foreach (PostVariant variant in media.VideoInfo.Variants)
                {
                    if (!config.Types.Contains(variant.ContentType))
                        continue;

                    string? formatType = _mediaVideoVariantPolicyService.GetFormatType(
                        variant.ContentType
                    );

                    if (formatType is null)
                        continue;

                    string url = variant.Url.Split('?')[0];
                    string videoFileName = Path.GetFileName(url);
                    string videoId = Path.GetFileNameWithoutExtension(videoFileName);
                    string? resolution = _mediaVideoVariantPolicyService.GetResolution(
                        formatType,
                        url
                    );

                    if (resolution is null)
                        throw new Exception();

                    MediaDownloadData builtVideo = _mediaDownloadDataBuilderService.Build(
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

                    AddDataDownload(post.Id, builtVideo, include: true, all, filtered);
                }
            }
        }
    }

    private void ProcessProfile(
        IReadOnlyList<MediaInput> posts,
        MediaDownloadProjectionRuleConfig config,
        Dictionary<string, MediaDownload> all,
        Dictionary<string, MediaDownload> filtered
    )
    {
        if (config.Dimensions is null || config.Sizes is null)
            return;

        List<Resolution> resolutions = BuildResolutions(config.Dimensions, config.Sizes);

        foreach (MediaInput post in posts)
        {
            string? url = post.Profile.ImageUrl;

            if (string.IsNullOrWhiteSpace(url))
                continue;

            string? fileName = Path.GetFileName(url);

            if (fileName is null)
                continue;

            string extension = Path.GetExtension(fileName).Replace(".", string.Empty);
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
        if (config.Dimensions is null || config.Sizes is null)
            return;

        List<Resolution> resolutions = BuildResolutions(config.Dimensions, config.Sizes);

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

    private void ApplyDedup(Dictionary<string, MediaDownload> downloads)
    {
        IReadOnlyList<MediaDownload> deduped = _mediaDuplicateFilterService.Filter(
            [.. downloads.Values]
        );

        downloads.Clear();

        foreach (MediaDownload download in deduped)
            downloads[download.Id] = download.Clone();
    }

    private static List<Resolution> BuildResolutions(
        IReadOnlyList<string> dimensions,
        IReadOnlyList<string> sizes
    ) =>
        [
            .. dimensions.Select(value => new Resolution(value, "dimension")),
            .. sizes.Select(value => new Resolution(value, "size")),
        ];

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
}
