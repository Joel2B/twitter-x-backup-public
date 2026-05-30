using Backup.Application.Posts.Ports;
using Backup.Infrastructure.Interfaces.Data.Posts;
using Backup.Infrastructure.Interfaces.Services.Posts;
using Backup.Infrastructure.Models.Config.Api;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Services.Posts;

internal sealed class PostServiceRecoveryCommand(
    ILogger<PostService> logger,
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

