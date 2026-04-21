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
        await RunPostRecoveryServices();
        await RunPostSources();
        await RunBulkServices();
        await RunMediaServices();
    }

    private async Task RunPostRecoveryServices()
    {
        Models.Config.FetchContext context = new() { Source = _config.Source.Clone() };

        foreach (IPostService service in _postServices)
        {
            using (_logger.LogTimer($"post recovery service: {service.GetType().Name}"))
                await service.Recover(context);
        }
    }

    private async Task RunPostSources()
    {
        if (!_config.Source.Enabled)
            return;

        List<Models.Config.Source> sources = _config
            .Sources.Where(source => source.Enabled)
            .ToList();

        foreach (Models.Config.Source source in sources)
        {
            Models.Config.FetchContext context = Models.Config.FetchContextFactory.Create(
                _config.Source,
                source
            );

            _logger.LogInfo("source: {source}", context.Source.Id);
            await RunPostServices(context);
        }
    }

    private async Task RunPostServices(Models.Config.FetchContext fetchContext)
    {
        foreach (IPostService service in _postServices)
        {
            using (_logger.LogTimer($"post service: {service.GetType().Name}"))
                await service.Download(fetchContext);
        }
    }

    private async Task RunBulkServices()
    {
        if (!_config.Bulk.Enabled)
            return;

        foreach (IBulkService service in _bulkServices)
        {
            using (_logger.LogTimer($"bulk service: {service.GetType().Name}"))
                await service.Download();
        }
    }

    private async Task RunMediaServices()
    {
        if (!_config.Medias.Enabled)
            return;

        foreach (IMediaService service in _mediaServices)
        {
            using (_logger.LogTimer($"media service: {service.GetType().Name}"))
                await service.Download();
        }
    }
}
