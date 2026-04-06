using AutoMapper;
using Backup.App.Interfaces.Data.Post;
using Backup.App.Interfaces.Services.Media;
using Backup.App.Interfaces.Services.Post;
using Backup.App.Models.Config.Request;
using Backup.App.Models.Media.Logging;
using Backup.App.Models.Post;
using Microsoft.Extensions.Logging;

namespace Backup.App.Services.Post;

public class PostRecovery(
    ILogger<PostRecovery> _logger,
    Models.Config.App _config,
    IMapper _mapper,
    IMediaLogger _mediaLogger,
    IPostDownloader _downloader,
    IPostParser _parser,
    IPostMerger _merger
) : IPostRecovery
{
    private readonly ILogger<PostRecovery> _logger = _logger;
    private readonly Models.Config.App _config = _config;
    private readonly IMapper _mapper = _mapper;
    private readonly IMediaLogger _mediaLogger = _mediaLogger;
    private readonly IPostDownloader _downloader = _downloader;
    private readonly IPostParser _parser = _parser;
    private readonly IPostMerger _merger = _merger;

    private IPostData? _postData;
    private IPostData PostData => _postData ?? throw new Exception("media data not initialized");

    private readonly CancellationTokenSource _tokenSource = new();
    private readonly List<Models.Post.Post> _postsCache = [];

    private int _count = 0;

    private string UserId =>
        _config.Source.Request.Query.Variables["userId"]?.ToString() ?? throw new Exception();

    public async Task Recovery(Dictionary<string, Models.Post.Post> posts, IPostData postData)
    {
        _postData = postData;

        try
        {
            await Download();

            _merger.Merge(UserId, _config.Source.Id, posts, _postsCache, new() { Index = false });
            _logger.LogInformation("post {post} merged", _postsCache.Count);

            await Save(posts);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    private async Task Download()
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

        if (!_config.Post.Recovery)
        {
            _logger.LogInformation("post recovery disabled");
            return;
        }

        if (ids.Count == 0)
            return;

        Request baseRequest = _config.Source.Request.Clone();
        Request request = _config.Api["TweetDetail"].Clone();

        _mapper.Map(request, baseRequest);
        request.Headers = baseRequest.Headers;

        foreach (string id in ids)
        {
            request.Query.Variables["focalTweetId"] = id;

            string response = await _downloader.Download(request, _tokenSource.Token);
            ParseResult result = _parser.Parse(UserId, _config.Source.Id, response);

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

        if (_postsCache.Count == 0)
            return;
    }

    private async Task Save(Dictionary<string, Models.Post.Post> posts)
    {
        if (_postsCache.Count == 0)
            return;

        _logger.LogInformation("Saving {data} data", posts.Values.Count);
        await PostData.Save([.. posts.Values]);
    }
}
