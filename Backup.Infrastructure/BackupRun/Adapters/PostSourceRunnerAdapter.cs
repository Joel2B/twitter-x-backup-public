using Backup.Application.BackupRun.Ports;
using Backup.Domain.BackupRun;
using Backup.Infrastructure.Posts.Abstractions.Services;
using Backup.Infrastructure.Logging;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.BackupRun.Adapters;

public class PostSourceRunnerAdapter(
    IEnumerable<IPostService> postServices,
    ILogger<PostSourceRunnerAdapter> logger
) : IPostSourceRunner
{
    private readonly IEnumerable<IPostService> _postServices = postServices;
    private readonly ILogger<PostSourceRunnerAdapter> _logger = logger;

    public async Task Run(string userId, BackupRunSourcePlan source)
    {
        var apiContext = BackupRunPlanMapper.ToApiContext(userId, source);

        _logger.LogInfo("source: {source}", apiContext.Id);

        foreach (IPostService service in _postServices)
        {
            using (_logger.LogTimer($"post service: {service.GetType().Name}"))
                await service.Download(apiContext);
        }
    }
}
