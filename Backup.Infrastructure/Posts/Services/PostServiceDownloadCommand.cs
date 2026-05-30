using Backup.Application.Posts.Ports;
using Backup.Infrastructure.Interfaces.Data.Posts;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Posts;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Services.Posts;

internal sealed class PostServiceDownloadCommand(
    ILogger<PostService> logger,
    IPostDownload download,
    IPostDomainData data,
    ApiContext context
) : IPostDownloadRuntimeCommand
{
    public void OnDownloadStarting() =>
        logger.LogInformation(data.Id, "downloading posts and pruning");

    public Task RunDownload() => download.Download(data, context);

    public Task RunPrune() => data.Prune();
}

