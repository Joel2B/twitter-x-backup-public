using Backup.App.Interfaces.Data.Post;
using Backup.App.Interfaces.Services.Media;
using Backup.App.Interfaces.Services.Post;
using Backup.App.Models.Config.Request;
using Backup.App.Models.Media.Logging;
using Backup.App.Models.Post;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Backup.App.Services.Post;

public class PostRecovery(
    ILogger<PostRecovery> _logger,
    Models.Config.App _config,
    IMediaLogger _mediaLogger,
    IPostDownloader _downloader,
    IPostParser _parser
) : IPostRecovery
{
    private const string RecoveryOrigin = "recovery";

    private readonly ILogger<PostRecovery> _logger = _logger;
    private readonly Models.Config.App _config = _config;
    private readonly IMediaLogger _mediaLogger = _mediaLogger;
    private readonly IPostDownloader _downloader = _downloader;
    private readonly IPostParser _parser = _parser;

    private readonly CancellationTokenSource _tokenSource = new();

    private Models.Config.FetchContext? _fetchContext;

    private Models.Config.FetchContext FetchContext =>
        _fetchContext ?? throw new Exception("FetchContext not initialized");

    private string UserId => FetchContext.UserId;

    public async Task Recovery(IPostData postData, Models.Config.FetchContext fetchContext)
    {
        _fetchContext = fetchContext;

        try
        {
            List<Models.Post.Post> posts = await Download(fetchContext);

            if (posts.Count == 0)
            {
                _logger.LogInformation("recovery has no posts to merge");
                return;
            }

            await postData.AddPosts(UserId, RecoveryOrigin, posts, new() { Index = false });
            _logger.LogInformation("post {post} merged", posts.Count);

            _logger.LogInformation("saving posts");
            await postData.Save();
        }
        catch (Exception ex)
        {
            _logger.LogError("Error: {error}", JsonConvert.SerializeObject(ex));
        }
    }

    private async Task<List<Models.Post.Post>> Download(Models.Config.FetchContext fetchContext)
    {
        List<Models.Post.Post> posts = [];
        List<Logs>? logs = await _mediaLogger.GetErrors();

        if (logs is null)
        {
            _logger.LogInformation("no posts errores");
            return posts;
        }

        List<string> ids = logs.Where(log =>
                log.Messages.Any(msg => msg.Message == "NotFound" || msg.Message == "Forbidden")
            )
            .Select(log => log.Id)
            .ToList();

        _logger.LogInformation("{count} posts with errors", ids.Count);

        if (!_config.Services.Recovery.Enabled)
        {
            _logger.LogInformation("post recovery disabled");
            return posts;
        }

        if (ids.Count == 0)
            return posts;

        Request? request = RequestMerge.Build(
            fetchContext.Source.Request,
            _config.Api,
            "TweetDetail"
        );

        if (request is null)
        {
            _logger.LogWarning("api 'TweetDetail' is disabled or not configured");
            return posts;
        }

        int _count = 0;

        foreach (string id in ids)
        {
            request.Query.Variables["focalTweetId"] = id;

            string response = await _downloader.Download(request, _tokenSource.Token);
            ParseResult result = _parser.Parse(UserId, RecoveryOrigin, response);

            if (result.Posts.Count == 0)
                continue;

            posts.Add(result.Posts[0]);
            await _mediaLogger.RemoveErrors([.. logs.Where(o => o.Id == result.Posts[0].Id)]);

            _logger.LogInformation("post {post} downloaded", id);
            await Task.Delay(5 * 1000);

            _count++;

            if (_count >= 10)
                break;
        }

        return posts;
    }
}
