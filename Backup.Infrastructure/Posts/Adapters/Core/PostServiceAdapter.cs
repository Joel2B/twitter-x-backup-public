using Backup.Application.Posts;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Posts.Abstractions.Data;
using Backup.Infrastructure.Posts.Abstractions.Services;
using Backup.Infrastructure.Posts.Models;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Posts.Adapters;

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

    public async Task Recover(
        UsersContext context,
        CancellationToken cancellationToken = default
    ) =>
        await _postRuntimeService.Recover(
            new PostServiceRecoveryCommandAdapter(_logger, _postRecovery, _postData, context),
            cancellationToken
        );

    public async Task Download(ApiContext context, CancellationToken cancellationToken = default) =>
        await _postRuntimeService.Download(
            new PostServiceDownloadCommandAdapter(_logger, _postDownload, _postData, context),
            cancellationToken
        );
}
