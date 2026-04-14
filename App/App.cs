using Backup.App.Extensions;
using Backup.App.Interfaces.Services.Media;
using Backup.App.Interfaces.Services.Post;
using Microsoft.Extensions.Logging;

namespace Backup.App;

public class App(
    ILogger<App> _logger,
    Models.Config.App _config,
    IEnumerable<IPostService> _postServices,
    IEnumerable<IBulkService> _bulkServices,
    IEnumerable<IMediaService> _mediaServices
)
{
    private readonly ILogger<App> _logger = _logger;
    private readonly Models.Config.App _config = _config;
    private readonly IEnumerable<IPostService> _postServices = _postServices;
    private readonly IEnumerable<IBulkService> _bulkServices = _bulkServices;
    private readonly IEnumerable<IMediaService> _mediaServices = _mediaServices;

    public async Task Backup()
    {
        foreach (Models.Config.Source source in _config.Sources)
        {
            if (!_config.Source.Enabled)
                break;

            if (!source.Enabled)
                continue;

            Models.Config.FetchContext fetchContext = Models.Config.FetchContextFactory.Create(
                _config.Source,
                source
            );

            _logger.LogInfo("source: {source}", fetchContext.Source.Id);

            foreach (IPostService service in _postServices)
            {
                using (_logger.LogTimer($"post service: {service.GetType().Name}"))
                    await service.Download(fetchContext);
            }
        }

        foreach (IBulkService service in _bulkServices)
        {
            if (!_config.Bulk.Enabled)
                break;

            using (_logger.LogTimer($"bulk service: {service.GetType().Name}"))
                await service.Download();
        }

        foreach (IMediaService service in _mediaServices)
        {
            if (!_config.Medias.Enabled)
                break;

            using (_logger.LogTimer($"media service: {service.GetType().Name}"))
                await service.Download();
        }
    }
}
