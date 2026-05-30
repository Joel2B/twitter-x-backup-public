using Backup.Application.BackupRun;
using Backup.Application.BackupRun.Ports;
using Backup.Domain.BackupRun;
using Backup.Infrastructure.Posts.Abstractions.Services;
using Backup.Infrastructure.Logging;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.BackupRun.Adapters;

public class PostRecoveryRunnerAdapter(
    IBackupRunStepExecutor stepExecutor,
    IEnumerable<IPostService> postServices,
    ILogger<PostRecoveryRunnerAdapter> logger
) : IPostRecoveryRunner
{
    private readonly IBackupRunStepExecutor _stepExecutor = stepExecutor;
    private readonly IEnumerable<IPostService> _postServices = postServices;
    private readonly ILogger<PostRecoveryRunnerAdapter> _logger = logger;

    public async Task Run(BackupRunUserPlan user)
    {
        var userContext = BackupRunPlanMapper.ToUsersContext(user);

        await _stepExecutor.Run(
            _postServices.Select(service => new PostRecoveryStep(_logger, service, userContext))
        );
    }

    private sealed class PostRecoveryStep(
        ILogger<PostRecoveryRunnerAdapter> logger,
        IPostService service,
        Backup.Infrastructure.Models.Config.Api.UsersContext userContext
    ) : IBackupRunStep
    {
        public async Task Run()
        {
            using (logger.LogTimer($"post recovery service: {service.GetType().Name}"))
                await service.Recover(userContext);
        }
    }
}
