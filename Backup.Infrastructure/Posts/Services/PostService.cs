using Backup.Application.Posts;
using Backup.Application.Posts.Ports;
using Backup.Infrastructure.Logging;
using Backup.Infrastructure.Interfaces.Services.Posts;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Posts;
using Microsoft.Extensions.Logging;
using Backup.Infrastructure.Interfaces.Data.Posts;

namespace Backup.Infrastructure.Services.Posts;

public class PostService(
    ILogger<PostService> _logger,
    IPostExecutionService postExecutionService,
    IPostDomainData _postData,
    IPostRecovery _postRecovery,
    IPostDownload _postDownload
) : IPostService
{
    private readonly ILogger<PostService> _logger = _logger;
    private readonly IPostExecutionService _postExecutionService = postExecutionService;
    private readonly IPostDomainData _postData = _postData;
    private readonly IPostRecovery _postRecovery = _postRecovery;
    private readonly IPostDownload _postDownload = _postDownload;

    public async Task Recover(UsersContext context)
    {
        IPostDomainData data = _postData;

        _logger.LogInformation(data.Id, "post data: {name}", data.GetType().Name);
        _logger.LogInformation(data.Id, "recovering posts in {data}", data.GetType().Name);

        await _postExecutionService.Recover(
            new RecoveryExecution(_postRecovery, data, context)
        );
    }

    public async Task Download(ApiContext context)
    {
        IPostDomainData data = _postData;

        _logger.LogInformation(data.Id, "downloading posts and pruning");
        await _postExecutionService.Download(
            new DownloadExecution(_postDownload, data, context)
        );
    }

    private sealed class RecoveryExecution(
        IPostRecovery recovery,
        IPostDomainData data,
        UsersContext context
    ) : IPostRecoveryExecution
    {
        public Task Recover() => recovery.Recovery(data, context);
    }

    private sealed class DownloadExecution(
        IPostDownload download,
        IPostDomainData data,
        ApiContext context
    ) : IPostDownloadExecution
    {
        public Task Download() => download.Download(data, context);

        public Task Prune() => data.Prune();
    }
}


