using Backup.Infrastructure.Core.Media;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Media;
using Backup.Infrastructure.Models.Posts;
using Backup.Infrastructure.Services.Media.Processors;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Services.Media;

public class MediaProcessing(ILogger<MediaProcessing> _logger, AppConfig _config) : IMediaProcessing
{
    private readonly ILogger<MediaProcessing> _logger = _logger;
    private readonly AppConfig _config = _config;

    private readonly Dictionary<string, Download> _all = [];
    private readonly Dictionary<string, Download> _filtered = [];

    public Task Process(List<MediaInput> posts)
    {
        MediaProcessorContext context = new(posts, _all, _filtered);

        List<MediaProcessor> processors =
        [
            new PhotoProcessor(_config.Medias.Photo, context),
            new GifProcessor(_config.Medias.Gif, context),
            new ProfileProcessor(_config.Medias.Profile, context),
            new BannerProcessor(_config.Medias.Banner, context),
            new VideoProcessor(_config.Medias.Video, context),
        ];

        foreach (MediaProcessor processor in processors)
        {
            _logger.LogInformation("Media processing: {Processor}", processor.GetType().Name);
            processor.Process();

            _logger.LogInformation("Filter duplicates");
            processor.FilterDuplicates();
        }

        return Task.CompletedTask;
    }

    public List<Download> GetMedia() => [.. _all.Values];

    public List<Download> GetFilteredMedia() => [.. _filtered.Values];
}
