using Backup.Application.Posts.Ports;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Posts.Abstractions.Data;
using Backup.Infrastructure.Posts.Models;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Posts.Adapters;

internal sealed class PostServiceDownloadCommandAdapter(
    ILogger logger,
    IPostDownload download,
    IPostDomainData data,
    ApiContext context
) : IPostDownloadRuntimeCommand
{
    public void OnDownloadStarting() =>
        logger.LogInformation(data.Id, "downloading posts and pruning");

    public Task RunDownload(CancellationToken cancellationToken = default) =>
        download.Download(data, context, cancellationToken);

    public Task RunPrune(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return data.Prune();
    }
}
