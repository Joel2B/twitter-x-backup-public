using Backup.App.Extensions;
using Backup.App.Interfaces.Data.Post;
using Backup.App.Interfaces.Services.Post;
using Backup.App.Models.Post;
using Microsoft.Extensions.Logging;

namespace Backup.App.Services.Post;

public class PostService(
    ILogger<PostService> _logger,
    IEnumerable<IPostData> _postData,
    IPostRecovery _postRecovery,
    IPostDownload _postDownload,
    IPostReplication _postReplication
) : IPostService
{
    private readonly ILogger<PostService> _logger = _logger;
    private readonly IEnumerable<IPostData> _postData = _postData;
    private readonly IPostRecovery _postRecovery = _postRecovery;
    private readonly IPostDownload _postDownload = _postDownload;
    private readonly IPostReplication _postReplication = _postReplication;

    public async Task Recover(Models.Config.FetchContext fetchContext)
    {
        IPostData data = _postData.First();

        _logger.LogInformation(data.Id, "post data: {name}", data.GetType().Name);
        _logger.LogInformation(data.Id, "recovering posts in {data}", data.GetType().Name);
        await _postRecovery.Recovery(data, fetchContext);
    }

    public async Task Download(Models.Config.FetchContext fetchContext)
    {
        IPostData data = _postData.First();

        _logger.LogInformation(data.Id, "downloading posts");
        await _postDownload.Download(data, fetchContext);

        _logger.LogInformation(data.Id, "pruning posts");
        await data.Prune();

        _logger.LogInformation(data.Id, "replicating posts");
        await _postReplication.Replicate(_postData);
    }
}
