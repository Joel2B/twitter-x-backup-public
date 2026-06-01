using Backup.Application.BackupRun;
using Backup.Application.BackupRun.Models;
using Backup.Application.BackupRun.Ports;
using Backup.Infrastructure.Logging;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.BackupRun.Adapters;

public class PostRecoveryRunnerAdapter(
    IBackupRunStepExecutor stepExecutor,
    IEnumerable<IPostRecoveryExecutionService> postRecoveryExecutionServices,
    ILogger<PostRecoveryRunnerAdapter> logger
) : IPostRecoveryRunner
{
    private readonly IBackupRunStepExecutor _stepExecutor = stepExecutor;
    private readonly IEnumerable<IPostRecoveryExecutionService> _postRecoveryExecutionServices =
        postRecoveryExecutionServices;
    private readonly ILogger<PostRecoveryRunnerAdapter> _logger = logger;

    public Task Run(BackupRunRecoveryExecution execution) =>
        _stepExecutor.Run(
            _postRecoveryExecutionServices.Select(service => new PostRecoveryStep(
                _logger,
                service,
                execution
            ))
        );

    private sealed class PostRecoveryStep(
        ILogger<PostRecoveryRunnerAdapter> logger,
        IPostRecoveryExecutionService service,
        BackupRunRecoveryExecution execution
    ) : IBackupRunStep
    {
        public async Task Run()
        {
            using (logger.LogTimer($"post recovery execution service: {service.GetType().Name}"))
                await service.Recover(execution);
        }
    }
}
