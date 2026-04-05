using Backup.App.Core.Media;
using Backup.App.Interfaces.Services.Media;
using Backup.App.Models.Media;
using Backup.App.Services.Media.Processors;
using Microsoft.Extensions.Logging;

namespace Backup.App.Services.Media;

public class MediaProcessing(
    ILogger<MediaProcessing> _logger,
    Models.Config.App _config,
    IMediaProcessingLogger _mediaProcessingLogger
) : IMediaProcessing
{
    private readonly ILogger<MediaProcessing> _logger = _logger;
    private readonly Models.Config.App _config = _config;
    private readonly IMediaProcessingLogger _mediaProcessingLogger = _mediaProcessingLogger;

    private readonly Dictionary<string, Download> _all = [];
    private readonly Dictionary<string, Download> _filtered = [];

    public async Task Process(List<Models.Post.Post> posts)
    {
        MediaProcessorContext context = new(posts, _all, _filtered, _mediaProcessingLogger);

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

            _logger.LogInformation("Save log");
            await processor.SaveLog();
        }

        await _mediaProcessingLogger.Prune();
    }

    public List<Download> GetMedia() => [.. _all.Values];

    public List<Download> GetFilteredMedia() => [.. _filtered.Values];
}
