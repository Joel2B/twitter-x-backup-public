using Backup.Application.BackupRun;
using Backup.Application.BackupRun.Ports;
using Backup.Infrastructure.Logging;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.BackupRun.Adapters;

public class MediaRunnerAdapter(
    IBackupRunStepExecutor stepExecutor,
    IEnumerable<IMediaExecutionService> mediaExecutionServices,
    ILogger<MediaRunnerAdapter> logger
) : IMediaRunner
{
    private readonly IBackupRunStepExecutor _stepExecutor = stepExecutor;
    private readonly IEnumerable<IMediaExecutionService> _mediaExecutionServices =
        mediaExecutionServices;
    private readonly ILogger<MediaRunnerAdapter> _logger = logger;

    public Task Run() =>
        _stepExecutor.Run(
            _mediaExecutionServices.Select(service => new MediaStep(_logger, service))
        );

    private sealed class MediaStep(
        ILogger<MediaRunnerAdapter> logger,
        IMediaExecutionService service
    ) : IBackupRunStep
    {
        public async Task Run()
        {
            using (logger.LogTimer($"media execution service: {service.GetType().Name}"))
                await service.Download();
        }
    }
}
