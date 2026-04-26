using Backup.App.Extensions;
using Backup.App.Interfaces.Services.Media;
using Backup.App.Interfaces.Services.Post;
using Backup.App.Models.Config.Api;
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
    private string UserId => _config.Services.User.Id;

    public async Task Backup()
    {
        await RunPostRecoveryServices();
        await RunPostSources();
        await RunBulkServices();
        await RunMediaServices();
    }

    private async Task RunPostRecoveryServices()
    {
        foreach (IPostService service in _postServices)
        {
            using (_logger.LogTimer($"post recovery service: {service.GetType().Name}"))
                await service.Recover(UserId);
        }
    }

    private async Task RunPostSources()
    {
        foreach (var kvp in _config.Fetch)
        {
            string apiId = kvp.Key;

            if (!_config.Api.TryGetValue(apiId, out Api? api))
                continue;

            if (!api.Enabled)
                continue;

            Models.Config.Request.Request request = api.Request.Clone();
            int count = kvp.Value.Count;

            ApiContext context = new()
            {
                Id = api.Id,
                Request = request,
                Count = count,
                UserId = UserId,
            };

            _logger.LogInfo("source: {source}", context.Id);
            await RunPostServices(context);
        }
    }

    private async Task RunPostServices(ApiContext context)
    {
        foreach (IPostService service in _postServices)
        {
            using (_logger.LogTimer($"post service: {service.GetType().Name}"))
                await service.Download(context);
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
