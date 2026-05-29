using Backup.Application.BackupRun.Models;
using Backup.Application.BackupRun.Ports;
using Backup.Infrastructure.Interfaces.Services.Posts;
using Backup.Infrastructure.Logging;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.BackupRun.Adapters;

public class PostRecoveryRunnerAdapter(
    IEnumerable<IPostService> postServices,
    ILogger<PostRecoveryRunnerAdapter> logger
) : IPostRecoveryRunner
{
    private readonly IEnumerable<IPostService> _postServices = postServices;
    private readonly ILogger<PostRecoveryRunnerAdapter> _logger = logger;

    public async Task Run(BackupRunUserPlan user)
    {
        var userContext = BackupRunPlanMapper.ToUsersContext(user);

        foreach (IPostService service in _postServices)
        {
            using (_logger.LogTimer($"post recovery service: {service.GetType().Name}"))
                await service.Recover(userContext);
        }
    }
}
