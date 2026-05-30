using Backup.Application.BackupRun.Ports;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Logging;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.BackupRun.Adapters;

public class MediaRunnerAdapter(
    IEnumerable<IMediaService> mediaServices,
    ILogger<MediaRunnerAdapter> logger
) : IMediaRunner
{
    private readonly IEnumerable<IMediaService> _mediaServices = mediaServices;
    private readonly ILogger<MediaRunnerAdapter> _logger = logger;

    public async Task Run()
    {
        foreach (IMediaService service in _mediaServices)
        {
            using (_logger.LogTimer($"media service: {service.GetType().Name}"))
                await service.Download();
        }
    }
}
