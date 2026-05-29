using Backup.App.Core.Media;
using Backup.App.Models.Config.Medias;
using Backup.App.Models.Media;
using Backup.App.Models.Media.Processors;
using Backup.App.Models.Posts;

namespace Backup.Infrastructure.Services.Media.Processors;

public class GifProcessor(GifConfig config, MediaProcessorContext context) : MediaProcessor(context)
{
    private readonly Backup.App.Utils.MediaFilter _filters = new(config.Thumb.Filters);

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
                posts.Medias is not null && posts.Medias.Any(media => media.Type == "animated_gif")
            )
            .Select(post => new { post.Id, post.Medias })
            .ToList();

        foreach (var post in posts)
        {
            if (post.Medias is null)
                continue;

            foreach (PostMedia media in post.Medias)
            {
                if (media.Type != "animated_gif")
                    continue;

                if (media.VideoInfo is null || media.VideoInfo.Variants is null)
                    continue;

                string id = Path.GetFileNameWithoutExtension(media.Url);

                List<Resolution> dimensions = config
                    .Thumb.Dimensions.Select(o => new Resolution() { Name = o, Type = "dimension" })
                    .ToList();

                List<Resolution> sizes = config
                    .Thumb.Sizes.Select(o => new Resolution() { Name = o, Type = "size" })
                    .ToList();

                List<Resolution> resolutions = [.. dimensions, .. sizes];

                foreach (string type in config.Thumb.Types)
                {
                    foreach (Resolution resolution in resolutions)
                    {
                        DataDownload dataDownload = Backup.App.Utils.MediaProcessor.GetData(
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

                        bool include = !_filters.IsExcluded(
                            Path.GetExtension(media.Url).Trim('.'),
                            type,
                            resolution.Name
                        );

                        AddDataDownload(post.Id, dataDownload, include);
                    }
                }

                foreach (PostVariant variant in media.VideoInfo.Variants)
                {
                    if (!config.Types.Contains(variant.ContentType))
                        continue;

                    string url = variant.Url.Split('?')[0];
                    string videoFileName = Path.GetFileName(url);
                    string VideoExtension = Path.GetExtension(videoFileName);
                    string VideoId = Path.GetFileNameWithoutExtension(videoFileName);

                    DataDownload dataDownload = Backup.App.Utils.MediaProcessor.GetData(
                        new()
                        {
                            PostId = post.Id,
                            MediaType = "gif",
                            Id = media.Id,
                            FormatType = "mp4",
                            ResolutionType = VideoId,
                            Name = "index",
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


