using Backup.Infrastructure.Logging;
using Backup.App.Interfaces.Data.Posts;
using Backup.App.Interfaces.Services.Posts;
using Backup.App.Models.Config.Api;
using Backup.App.Models.Posts;
using Microsoft.Extensions.Logging;

namespace Backup.App.Services.Posts;

public class PostService(
    ILogger<PostService> _logger,
    IPostData _postData,
    IPostRecovery _postRecovery,
    IPostDownload _postDownload
) : IPostService
{
    private readonly ILogger<PostService> _logger = _logger;
    private readonly IPostData _postData = _postData;
    private readonly IPostRecovery _postRecovery = _postRecovery;
    private readonly IPostDownload _postDownload = _postDownload;

    public async Task Recover(UsersContext context)
    {
        IPostData data = _postData;

        _logger.LogInformation(data.Id, "post data: {name}", data.GetType().Name);
        _logger.LogInformation(data.Id, "recovering posts in {data}", data.GetType().Name);
        await _postRecovery.Recovery(data, context);
    }

    public async Task Download(ApiContext context)
    {
        IPostData data = _postData;

        _logger.LogInformation(data.Id, "downloading posts");
        await _postDownload.Download(data, context);

        _logger.LogInformation(data.Id, "pruning posts");
        await data.Prune();
    }
}
