using Backup.Application.BackupRun;
using Backup.Application.BackupRun.Models;
using Backup.Application.BackupRun.Ports;
using Backup.Infrastructure.Logging;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.BackupRun.Adapters;

public class PostSourceRunnerAdapter(
    IBackupRunStepExecutor stepExecutor,
    IEnumerable<IPostSourceExecutionService> postSourceExecutionServices,
    ILogger<PostSourceRunnerAdapter> logger
) : IPostSourceRunner
{
    private readonly IBackupRunStepExecutor _stepExecutor = stepExecutor;
    private readonly IEnumerable<IPostSourceExecutionService> _postSourceExecutionServices =
        postSourceExecutionServices;
    private readonly ILogger<PostSourceRunnerAdapter> _logger = logger;

    public async Task Run(BackupRunSourceExecution execution)
    {
        _logger.LogInfo("source: {source}", execution.ApiId);

        await _stepExecutor.Run(
            _postSourceExecutionServices.Select(service => new PostSourceStep(
                _logger,
                service,
                execution
            ))
        );
    }

    private sealed class PostSourceStep(
        ILogger<PostSourceRunnerAdapter> logger,
        IPostSourceExecutionService service,
        BackupRunSourceExecution execution
    ) : IBackupRunStep
    {
        public async Task Run()
        {
            using (logger.LogTimer($"post source execution service: {service.GetType().Name}"))
                await service.Download(execution);
        }
    }
}
