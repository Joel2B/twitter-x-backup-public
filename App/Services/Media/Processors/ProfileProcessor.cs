using Backup.App.Core.Media;
using Backup.App.Models.Media;
using Backup.App.Models.Media.Processors;

namespace Backup.App.Services.Media.Processors;

public class ProfileProcessor(Models.Config.Medias.Profile config, MediaProcessorContext context)
    : MediaProcessor(context)
{
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

        foreach (Models.Post.MediaInput post in Context.Posts)
        {
            string? url = post.Profile.ImageUrl;

            if (url is null)
                continue;

            string? fileName = Path.GetFileName(url);

            if (fileName is null)
                continue;

            string extension = Path.GetExtension(fileName).Replace(".", string.Empty);
            string id = fileName.Split("_normal")[0];

            foreach (Resolution resolution in resolutions)
            {
                DataDownload dataDownload = Utils.MediaProcessor.GetData(
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
                        Options = { Query = false },
                    }
                );

                AddDataDownload(post.Id, dataDownload, true);
            }
        }
    }
}
