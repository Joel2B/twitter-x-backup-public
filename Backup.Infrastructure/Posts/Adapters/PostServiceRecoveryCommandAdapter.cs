using Backup.Application.Posts.Ports;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Posts.Abstractions.Data;
using Backup.Infrastructure.Posts.Abstractions.Services;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Posts.Adapters;

internal sealed class PostServiceRecoveryCommandAdapter(
    ILogger logger,
    IPostRecovery recovery,
    IPostDomainData data,
    UsersContext context
) : IPostRecoveryRuntimeCommand
{
    public void OnRecoveryStarting()
    {
        logger.LogInformation(data.Id, "post data: {name}", data.GetType().Name);
        logger.LogInformation(data.Id, "recovering posts in {data}", data.GetType().Name);
    }

    public Task RunRecovery() => recovery.Recovery(data, context);
}
