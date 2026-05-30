using Backup.Application.BackupRun;
using Backup.Application.BackupRun.Ports;
using Backup.Domain.BackupRun;
using Backup.Infrastructure.Posts.Abstractions.Services;
using Backup.Infrastructure.Logging;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.BackupRun.Adapters;

public class PostSourceRunnerAdapter(
    IBackupRunStepExecutor stepExecutor,
    IEnumerable<IPostService> postServices,
    ILogger<PostSourceRunnerAdapter> logger
) : IPostSourceRunner
{
    private readonly IBackupRunStepExecutor _stepExecutor = stepExecutor;
    private readonly IEnumerable<IPostService> _postServices = postServices;
    private readonly ILogger<PostSourceRunnerAdapter> _logger = logger;

    public async Task Run(string userId, BackupRunSourcePlan source)
    {
        var apiContext = BackupRunPlanMapper.ToApiContext(userId, source);

        _logger.LogInfo("source: {source}", apiContext.Id);

        await _stepExecutor.Run(
            _postServices.Select(service => new PostSourceStep(_logger, service, apiContext))
        );
    }

    private sealed class PostSourceStep(
        ILogger<PostSourceRunnerAdapter> logger,
        IPostService service,
        Backup.Infrastructure.Models.Config.Api.ApiContext apiContext
    ) : IBackupRunStep
    {
        public async Task Run()
        {
            using (logger.LogTimer($"post service: {service.GetType().Name}"))
                await service.Download(apiContext);
        }
    }
}
