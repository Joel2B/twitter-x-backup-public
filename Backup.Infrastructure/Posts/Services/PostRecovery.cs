using Backup.Infrastructure.Interfaces.Data.Posts;
using Backup.Infrastructure.Interfaces.Services.Media;
using Backup.Infrastructure.Interfaces.Services.Posts;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Config.ApiRequest;
using Backup.Infrastructure.Models.Media.Logging;
using Backup.Infrastructure.Models.Posts;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Backup.Infrastructure.Services.Posts;

public class PostRecovery(
    ILogger<PostRecovery> _logger,
    AppConfig _config,
    IMediaLogger _mediaLogger,
    IPostDownloader _downloader,
    IPostParser _parser
) : IPostRecovery
{
    private const string RecoveryOrigin = "recovery";

    private readonly ILogger<PostRecovery> _logger = _logger;
    private readonly AppConfig _config = _config;
    private readonly IMediaLogger _mediaLogger = _mediaLogger;
    private readonly IPostDownloader _downloader = _downloader;
    private readonly IPostParser _parser = _parser;

    private readonly CancellationTokenSource _tokenSource = new();

    public async Task Recovery(IPostData postData, UsersContext context)
    {
        try
        {
            List<Post> posts = await Download(context);

            if (posts.Count == 0)
            {
                _logger.LogInformation("recovery has no posts to merge");
                return;
            }

            await postData.AddPosts(context.UserId, RecoveryOrigin, posts, new() { Index = false });
            _logger.LogInformation("post {post} merged", posts.Count);

            _logger.LogInformation("saving posts");
            await postData.Save();
        }
        catch (Exception ex)
        {
            _logger.LogError("Error: {error}", JsonConvert.SerializeObject(ex));
        }
    }

    private async Task<List<Post>> Download(UsersContext context)
    {
        List<Post> posts = [];
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

        Request? request = RequestMerge.Build(context.Api, "TweetDetail");

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
            ParseResult result = _parser.Parse(context.UserId, RecoveryOrigin, response);

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


