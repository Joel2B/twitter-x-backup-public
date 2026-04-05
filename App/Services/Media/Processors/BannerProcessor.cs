using Backup.App.Core.Media;
using Backup.App.Models.Media;
using Backup.App.Models.Media.Processors;

namespace Backup.App.Services.Media.Processors;

public class BannerProcessor(Models.Config.Medias.Banner config, MediaProcessorContext context)
    : MediaProcessor(context)
{
    public override void Process()
    {
        if (config.Dimensions is null || config.Sizes is null)
            return;

        foreach (Models.Post.Post post in Context.Posts)
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
                DataDownload dataDownload = Utils.MediaProcessor.GetData(
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
                        Options = { Query = false },
                    }
                );

                AddDataDownload(post.Id, dataDownload, true);
            }
        }
    }
}
