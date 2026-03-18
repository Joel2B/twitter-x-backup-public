using AutoMapper;
using Backup.App.Extensions;
using Backup.App.Interfaces.Services;
using Backup.App.Interfaces.Services.Media;
using Backup.App.Interfaces.Services.Post;
using Microsoft.Extensions.Logging;

namespace Backup.App;

public class App(
    ILogger<App> _logger,
    Models.Config.App _config,
    IMapper _mapper,
    IEnumerable<IService> _services
)
{
    private readonly ILogger<App> _logger = _logger;
    private readonly Models.Config.App _config = _config;
    private readonly IMapper _mapper = _mapper;
    private readonly IEnumerable<IService> _services = _services;

    public async Task Backup()
    {
        foreach (Models.Config.Source source in _config.Sources)
        {
            if (!_config.Source.Enabled)
                break;

            if (!source.Enabled)
                continue;

            _mapper.Map(source, _config.Source);
            _logger.LogInfo("source: {source}", _config.Source.Id);

            foreach (IService service in _services.OfType<IPostService>())
            {
                using (_logger.LogTimer($"post service: {service.GetType().Name}"))
                    await service.Download();
            }
        }

        foreach (IService service in _services.OfType<IBulkService>())
        {
            if (!_config.Bulk.Enabled)
                break;

            using (_logger.LogTimer($"bulk service: {service.GetType().Name}"))
                await service.Download();
        }

        foreach (IService service in _services.OfType<IMediaService>())
        {
            if (!_config.Medias.Enabled)
                break;

            using (_logger.LogTimer($"media service: {service.GetType().Name}"))
                await service.Download();
        }
    }
}
