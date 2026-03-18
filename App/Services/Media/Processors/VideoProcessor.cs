using System.Text.RegularExpressions;
using Backup.App.Core.Media;
using Backup.App.Models.Media;
using Backup.App.Models.Media.Processors;
using Backup.App.Models.Post;

namespace Backup.App.Services.Media.Processors;

public class VideoProcessor(Models.Config.Medias.Video config, MediaProcessorContext context)
    : MediaProcessor(context)
{
    private readonly Utils.MediaFilter _filters = new(config.Thumb.Filters);

    public override void Process()
    {
        if (
            config.Thumb.Types is null
            || config.Thumb.Dimensions is null
            || config.Thumb.Sizes is null
            || config.Types is null
        )
            return;

        var posts = Context
            .Posts.Where(posts =>
                posts.Medias is not null && posts.Medias.Any(media => media.Type == "video")
            )
            .Select(post => new { post.Id, post.Medias })
            .ToList();

        List<Resolution> dimensions = config
            .Thumb.Dimensions.Select(o => new Resolution() { Name = o, Type = "dimension" })
            .ToList();

        List<Resolution> sizes = config
            .Thumb.Sizes.Select(o => new Resolution() { Name = o, Type = "size" })
            .ToList();

        List<Resolution> resolutions = [.. dimensions, .. sizes];

        foreach (var post in posts)
        {
            if (post.Medias is null)
                continue;

            List<DataDownload> data = [];

            foreach (Models.Post.Media media in post.Medias)
            {
                if (
                    media.Type != "video"
                    || media.VideoInfo is null
                    || media.VideoInfo.Variants is null
                )
                    continue;

                string thumbUrl = media.Url;
                string fileName = Path.GetFileName(thumbUrl);
                string extension = Path.GetExtension(fileName);
                string id = Path.GetFileNameWithoutExtension(fileName);

                foreach (string type in config.Thumb.Types)
                {
                    foreach (Resolution resolution in resolutions)
                    {
                        DataDownload dataDownload = Utils.MediaProcessor.GetData(
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

                        bool include = !_filters.IsExcluded(
                            Path.GetExtension(media.Url).Trim('.'),
                            type,
                            resolution.Name
                        );

                        AddDataDownload(post.Id, dataDownload, include);
                    }
                }

                foreach (Variant variant in media.VideoInfo.Variants)
                {
                    if (!config.Types.Contains(variant.ContentType))
                        continue;

                    string? formatType = variant.ContentType switch
                    {
                        "application/x-mpegURL" => "m3u8",
                        "video/mp4" => "mp4",
                        _ => null,
                    };

                    if (formatType is null)
                        continue;

                    string url = variant.Url.Split('?')[0];
                    string videoFileName = Path.GetFileName(url);
                    string VideoId = Path.GetFileNameWithoutExtension(videoFileName);
                    string? resolution = null;

                    if (formatType == "m3u8")
                        resolution = "master";

                    if (formatType == "mp4")
                        resolution = Regex.Match(url, @"/(\d+x\d+)/").Groups[1].Value;

                    if (resolution is null)
                        throw new Exception();

                    DataDownload dataDownload = Utils.MediaProcessor.GetData(
                        new()
                        {
                            PostId = post.Id,
                            MediaType = "video",
                            Id = media.Id,
                            FormatType = formatType,
                            ResolutionType = VideoId,
                            Name = resolution,
                            Url = url,
                            Options = { Query = false },
                        }
                    );

                    AddDataDownload(post.Id, dataDownload, true);
                }
            }
        }
    }
}
