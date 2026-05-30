using Backup.Application.Posts;
using Backup.Infrastructure.Interfaces.Data.Posts;
using Backup.Infrastructure.Interfaces.Services.Posts;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Posts;
using Backup.Infrastructure.Posts.Adapters;
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
            new PostServiceRecoveryCommandAdapter(_logger, _postRecovery, _postData, context)
        );

    public async Task Download(ApiContext context) =>
        await _postRuntimeService.Download(
            new PostServiceDownloadCommandAdapter(_logger, _postDownload, _postData, context)
        );
}


