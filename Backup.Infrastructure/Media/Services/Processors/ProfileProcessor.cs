using Backup.Infrastructure.Core.Media;
using Backup.Application.Media;
using Backup.Infrastructure.Models.Config.Medias;
using Backup.Infrastructure.Media.Models;
using Backup.Infrastructure.Media.Models.Processors;
using Backup.Infrastructure.Posts.Models;

namespace Backup.Infrastructure.Media.Services.Processors;

public class ProfileProcessor(
    ProfileConfig config,
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

        List<Resolution> dimensions = config
            .Dimensions.Select(o => new Resolution() { Name = o, Type = "dimension" })
            .ToList();

        List<Resolution> sizes = config
            .Sizes.Select(o => new Resolution() { Name = o, Type = "size" })
            .ToList();

        List<Resolution> resolutions = [.. dimensions, .. sizes];

        foreach (MediaInput post in Context.Posts)
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
                Backup.Application.Media.Models.MediaDownloadData built = _mediaDownloadDataBuilderService.Build(
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
                DataDownload dataDownload = new() { Url = built.Url, Path = built.Path };

                AddDataDownload(post.Id, dataDownload, true);
            }
        }
    }
}
