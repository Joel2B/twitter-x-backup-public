using Backup.Application.BackupRun;
using Backup.Application.BackupRun.Ports;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Logging;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.BackupRun.Adapters;

public class MediaRunnerAdapter(
    IBackupRunStepExecutor stepExecutor,
    IEnumerable<IMediaService> mediaServices,
    ILogger<MediaRunnerAdapter> logger
) : IMediaRunner
{
    private readonly IBackupRunStepExecutor _stepExecutor = stepExecutor;
    private readonly IEnumerable<IMediaService> _mediaServices = mediaServices;
    private readonly ILogger<MediaRunnerAdapter> _logger = logger;

    public Task Run() =>
        _stepExecutor.Run(_mediaServices.Select(service => new MediaStep(_logger, service)));

    private sealed class MediaStep(ILogger<MediaRunnerAdapter> logger, IMediaService service)
        : IBackupRunStep
    {
        public async Task Run()
        {
            using (logger.LogTimer($"media service: {service.GetType().Name}"))
                await service.Download();
        }
    }
}
