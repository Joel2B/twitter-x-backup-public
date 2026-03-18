using Backup.App.Core.Media;
using Backup.App.Models.Media;
using Backup.App.Models.Media.Processors;

namespace Backup.App.Services.Media.Processors;

public class PhotoProcessor(Models.Config.Medias.Photo config, MediaProcessorContext context)
    : MediaProcessor(context)
{
    private readonly Utils.MediaFilter _filters = new(config.Filters);

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

            foreach (Models.Post.Media media in post.Medias)
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
                        DataDownload dataDownload = Utils.MediaProcessor.GetData(
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

                        bool include = !_filters.IsExcluded(
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
