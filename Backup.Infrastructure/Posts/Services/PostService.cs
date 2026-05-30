using Backup.Application.Posts;
using Backup.Application.Posts.Ports;
using Backup.Infrastructure.Logging;
using Backup.Infrastructure.Interfaces.Data.Posts;
using Backup.Infrastructure.Interfaces.Services.Posts;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Posts;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Services.Posts;

public class PostService(
    ILogger<PostService> _logger,
    IPostRuntimeService postRuntimeService,
    IPostDomainData _postData,
    IPostRecovery _postRecovery,
    IPostDownload _postDownload
) : IPostService
{
    private readonly ILogger<PostService> _logger = _logger;
    private readonly IPostRuntimeService _postRuntimeService = postRuntimeService;
    private readonly IPostDomainData _postData = _postData;
    private readonly IPostRecovery _postRecovery = _postRecovery;
    private readonly IPostDownload _postDownload = _postDownload;

    public async Task Recover(UsersContext context) =>
        await _postRuntimeService.Recover(
            new RecoveryCommand(_logger, _postRecovery, _postData, context)
        );

    public async Task Download(ApiContext context) =>
        await _postRuntimeService.Download(
            new DownloadCommand(_logger, _postDownload, _postData, context)
        );

    private sealed class RecoveryCommand(
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

    private sealed class DownloadCommand(
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
}


