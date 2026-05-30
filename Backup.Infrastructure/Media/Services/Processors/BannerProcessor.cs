using Backup.Infrastructure.Core.Media;
using Backup.Application.Media;
using Backup.Infrastructure.Models.Config.Medias;
using Backup.Infrastructure.Media.Models;
using Backup.Infrastructure.Media.Models.Processors;
using Backup.Infrastructure.Posts.Models;

namespace Backup.Infrastructure.Media.Services.Processors;

public class BannerProcessor(
    BannerConfig config,
    MediaProcessorContext context,
    IMediaDownloadDataBuilderService mediaDownloadDataBuilderService
)
    : MediaProcessor(context)
{
    private readonly IMediaDownloadDataBuilderService _mediaDownloadDataBuilderService =
        mediaDownloadDataBuilderService;

    public override void Process()
    {
        if (config.Dimensions is null || config.Sizes is null)
            return;

        foreach (MediaInput post in Context.Posts)
        {
            string? url = post.Profile.BannerUrl;

            if (string.IsNullOrEmpty(url))
                continue;

            string id = Path.GetFileName(url);

            List<Resolution> dimensions = config
                .Dimensions.Select(o => new Resolution() { Name = o, Type = "dimension" })
                .ToList();

            List<Resolution> sizes = config
                .Sizes.Select(o => new Resolution() { Name = o, Type = "size" })
                .ToList();

            List<Resolution> resolutions = [.. dimensions, .. sizes];

            foreach (Resolution resolution in resolutions)
            {
                Backup.Application.Media.Models.MediaDownloadData built = _mediaDownloadDataBuilderService.Build(
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
                DataDownload dataDownload = new() { Url = built.Url, Path = built.Path };

                AddDataDownload(post.Id, dataDownload, true);
            }
        }
    }
}
