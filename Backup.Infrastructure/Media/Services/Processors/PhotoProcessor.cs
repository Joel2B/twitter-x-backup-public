using Backup.Infrastructure.Core.Media;
using Backup.Application.Media;
using Backup.Application.Media.Filter;
using Backup.Infrastructure.Models.Config.Medias;
using Backup.Infrastructure.Media.Models;
using Backup.Infrastructure.Media.Models.Processors;

namespace Backup.Infrastructure.Media.Services.Processors;

public class PhotoProcessor(
    PhotoConfig config,
    MediaProcessorContext context,
    IMediaDownloadFilterPolicyService downloadFilterPolicyService,
    IMediaDownloadDataBuilderService mediaDownloadDataBuilderService
)
    : MediaProcessor(context)
{
    private readonly IMediaDownloadFilterPolicyService _downloadFilterPolicyService =
        downloadFilterPolicyService;
    private readonly IReadOnlyList<MediaExclusionRule> _filters = downloadFilterPolicyService.Parse(
        config.Filters
    );
    private readonly IMediaDownloadDataBuilderService _mediaDownloadDataBuilderService =
        mediaDownloadDataBuilderService;

    public override void Process()
    {
        if (config.Types is null || config.Dimensions is null || config.Sizes is null)
            return;

        var photos = Context
            .Posts.Select(post => new
            {
                post.Id,
                Medias = post.Medias?.Where(media => media.Type == "photo").ToList(),
            })
            .ToList();

        foreach (var post in photos)
        {
            if (post.Medias is null)
                continue;

            foreach (var media in post.Medias)
            {
                string id = Path.GetFileNameWithoutExtension(media.Url);

                List<Resolution> dimensions = config
                    .Dimensions.Select(o => new Resolution() { Name = o, Type = "dimension" })
                    .ToList();

                List<Resolution> sizes = config
                    .Sizes.Select(o => new Resolution() { Name = o, Type = "size" })
                    .ToList();

                List<Resolution> resolutions = [.. dimensions, .. sizes];

                foreach (string type in config.Types)
                {
                    foreach (Resolution resolution in resolutions)
                    {
                        Backup.Application.Media.Models.MediaDownloadData built = _mediaDownloadDataBuilderService.Build(
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
                        DataDownload dataDownload = new() { Url = built.Url, Path = built.Path };

                        bool include = !_downloadFilterPolicyService.IsExcluded(
                            _filters,
                            Path.GetExtension(media.Url).Trim('.'),
                            type,
                            resolution.Name
                        );

                        AddDataDownload(post.Id, dataDownload, include);
                    }
                }
            }
        }
    }
}
