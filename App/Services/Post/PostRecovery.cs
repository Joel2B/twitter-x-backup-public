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
    private readonly ILogger<PostRecovery> _logger = _logger;
    private readonly Models.Config.App _config = _config;
    private readonly IMediaLogger _mediaLogger = _mediaLogger;
    private readonly IPostDownloader _downloader = _downloader;
    private readonly IPostParser _parser = _parser;

    private readonly CancellationTokenSource _tokenSource = new();
    private readonly List<Models.Post.Post> _postsCache = [];

    private Models.Config.FetchContext? _fetchContext;

    private Models.Config.FetchContext FetchContext =>
        _fetchContext ?? throw new Exception("FetchContext not initialized");

    private string UserId => FetchContext.UserId;

    public async Task Recovery(IPostData postData, Models.Config.FetchContext fetchContext)
    {
        _fetchContext = fetchContext;

        try
        {
            await Download(fetchContext);

            if (_postsCache.Count == 0)
            {
                _logger.LogInformation("recovery has no posts to merge");
                return;
            }

            _logger.LogInformation("recovery loaded {count} posts", await postData.GetCount());

            await postData.AddPosts(
                UserId,
                fetchContext.Source.Id,
                _postsCache,
                new() { Index = false }
            );

            _logger.LogInformation("post {post} merged", _postsCache.Count);

            _logger.LogInformation("saving posts");
            await postData.Save();
        }
        catch (Exception ex)
        {
            _logger.LogError("Error: {error}", JsonConvert.SerializeObject(ex));
        }
    }

    private async Task Download(Models.Config.FetchContext fetchContext)
    {
        if (_postsCache.Count > 0)
        {
            _logger.LogInformation("using the cache");
            return;
        }

        List<Logs>? logs = await _mediaLogger.GetErrors();

        if (logs is null)
        {
            _logger.LogInformation("no posts errores");
            return;
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
            return;
        }

        if (ids.Count == 0)
            return;

        Request? request = RequestMerge.Build(
            fetchContext.Source.Request,
            _config.Api,
            "TweetDetail"
        );

        if (request is null)
        {
            _logger.LogWarning("api 'TweetDetail' is disabled or not configured");
            return;
        }

        int _count = 0;

        foreach (string id in ids)
        {
            request.Query.Variables["focalTweetId"] = id;

            string response = await _downloader.Download(request, _tokenSource.Token);
            ParseResult result = _parser.Parse(UserId, fetchContext.Source.Id, response);

            if (result.Posts.Count == 0)
                continue;

            _postsCache.Add(result.Posts[0]);
            await _mediaLogger.RemoveErrors([.. logs.Where(o => o.Id == result.Posts[0].Id)]);

            _logger.LogInformation("post {post} downloaded", id);
            await Task.Delay(5 * 1000);

            _count++;

            if (_count >= 10)
                break;
        }
    }
}
