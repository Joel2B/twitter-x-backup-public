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

    public async Task Backup()
    {
        IReadOnlyList<UsersContext> contexts = _config.UsersContext;

        foreach (UsersContext context in contexts)
        {
            _logger.LogInfo("running backup for user id: {userId}", context.UserId);

            await RunPostSources(context);
        }

        await RunPostRecoveryServices(contexts[0]);
        await RunBulkServices(contexts[0]);
        await RunMediaServices();
    }

    private async Task RunPostRecoveryServices(UsersContext context)
    {
        foreach (IPostService service in _postServices)
        {
            using (_logger.LogTimer($"post recovery service: {service.GetType().Name}"))
                await service.Recover(context);
        }
    }

    private async Task RunPostSources(UsersContext context)
    {
        foreach (var kvp in _config.Fetch)
        {
            string apiId = kvp.Key;

            if (!context.Api.TryGetValue(apiId, out Api? api))
                continue;

            if (!api.Enabled)
                continue;

            Models.Config.Request.Request request = api.Request.Clone();
            int count = kvp.Value.Count;

            ApiContext apiContext = new()
            {
                Id = api.Id,
                Request = request,
                Count = count,
                UserId = context.UserId,
            };

            _logger.LogInfo("source: {source}", apiContext.Id);
            await RunPostServices(apiContext);
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

    private async Task RunBulkServices(UsersContext context)
    {
        if (!_config.Bulk.Enabled)
            return;

        foreach (IBulkService service in _bulkServices)
        {
            using (_logger.LogTimer($"bulk service: {service.GetType().Name}"))
                await service.Download(context);
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
