using Backup.Application.Posts;
using Backup.Application.Posts.Models;
using Backup.Infrastructure.Interfaces.Data.Posts;
using Backup.Infrastructure.Interfaces.Services.Media;
using Backup.Infrastructure.Interfaces.Services.Posts;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Config.ApiRequest;
using Backup.Infrastructure.Models.Media.Logging;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using DomainPost = Backup.Domain.Posts.Post;
using ParseResult = Backup.Domain.Posts.ParseResult;

namespace Backup.Infrastructure.Services.Posts;

public class PostRecovery(
    ILogger<PostRecovery> _logger,
    AppConfig _config,
    IPostRecoverySelectionService recoverySelectionService,
    IMediaLogger _mediaLogger,
    IPostDownloader _downloader,
    IPostDomainParser _parser
) : IPostRecovery
{
    private const string RecoveryOrigin = "recovery";

    private readonly ILogger<PostRecovery> _logger = _logger;
    private readonly AppConfig _config = _config;
    private readonly IPostRecoverySelectionService _recoverySelectionService = recoverySelectionService;
    private readonly IMediaLogger _mediaLogger = _mediaLogger;
    private readonly IPostDownloader _downloader = _downloader;
    private readonly IPostDomainParser _parser = _parser;

    public async Task Recovery(IPostDomainData postData, UsersContext context)
    {
        try
        {
            using CancellationTokenSource tokenSource = new();
            List<DomainPost> posts = await Download(context, tokenSource.Token);

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

    private async Task<List<DomainPost>> Download(
        UsersContext context,
        CancellationToken cancellationToken
    )
    {
        List<DomainPost> posts = [];
        List<Logs>? logs = await _mediaLogger.GetErrors();

        if (logs is null)
        {
            _logger.LogInformation("no posts errores");
            return posts;
        }

        IReadOnlyCollection<PostRecoveryLog> recoveryLogs = logs
            .Select(log => new PostRecoveryLog
            {
                PostId = log.Id,
                Messages = log.Messages.Select(message => message.Message).ToList(),
            })
            .ToList();
        PostRecoverySelection selection = _recoverySelectionService.Select(
            _config.Services.Recovery.Enabled,
            recoveryLogs,
            maxPosts: 10
        );

        if (!selection.IsRecoveryEnabled)
        {
            _logger.LogInformation("post recovery disabled");
            return posts;
        }

        _logger.LogInformation("{count} posts with errors", selection.PostIds.Count);

        if (selection.PostIds.Count == 0)
            return posts;

        Request? request = RequestMerge.Build(context.Api, "TweetDetail");

        if (request is null)
        {
            _logger.LogWarning("api 'TweetDetail' is disabled or not configured");
            return posts;
        }

        foreach (string id in selection.PostIds)
        {
            request.Query.Variables["focalTweetId"] = id;

            string response = await _downloader.Download(request, cancellationToken);
            ParseResult result = _parser.Parse(context.UserId, RecoveryOrigin, response);

            if (result.Posts.Count == 0)
                continue;

            posts.Add(result.Posts[0]);
            await _mediaLogger.RemoveErrors([.. logs.Where(o => o.Id == result.Posts[0].Id)]);

            _logger.LogInformation("post {post} downloaded", id);
            await Task.Delay(5 * 1000);
        }

        return posts;
    }
}


