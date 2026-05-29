using Backup.Application.BackupRun.Ports;
using Backup.Infrastructure.Interfaces.Services.Posts;
using Backup.Infrastructure.Logging;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Api;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.BackupRun.Adapters;

public class PostRecoveryRunnerAdapter(
    AppConfig config,
    IEnumerable<IPostService> postServices,
    ILogger<PostRecoveryRunnerAdapter> logger
) : IPostRecoveryRunner
{
    private readonly AppConfig _config = config;
    private readonly IEnumerable<IPostService> _postServices = postServices;
    private readonly ILogger<PostRecoveryRunnerAdapter> _logger = logger;

    public async Task Run(string userId)
    {
        UsersContext? userContext = _config.UsersContext.FirstOrDefault(context =>
            context.UserId == userId
        );

        if (userContext is null)
            return;

        foreach (IPostService service in _postServices)
        {
            using (_logger.LogTimer($"post recovery service: {service.GetType().Name}"))
                await service.Recover(userContext);
        }
    }
}
