using Backup.Application.Posts;
using Backup.Application.Posts.Models;
using Backup.Application.Posts.Ports;
using Backup.Infrastructure.Interfaces.Data.Posts;
using Backup.Infrastructure.Interfaces.Services.Media;
using Backup.Infrastructure.Interfaces.Services.Posts;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Config.ApiRequest;
using Backup.Infrastructure.Models.Media.Logging;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using LogEntry = Backup.Infrastructure.Models.Media.Logging.Logs;
using ParseResult = Backup.Domain.Posts.ParseResult;

namespace Backup.Infrastructure.Services.Posts;

public class PostRecovery(
    ILogger<PostRecovery> _logger,
    AppConfig _config,
    IPostRecoveryCommandService postRecoveryCommandService,
    IPostTweetDetailRequestFactory tweetDetailRequestFactory,
    IMediaLogger _mediaLogger,
    IPostDownloader _downloader,
    IPostDomainParser _parser
) : IPostRecovery
{
    private const string RecoveryOrigin = "recovery";

    private readonly ILogger<PostRecovery> _logger = _logger;
    private readonly AppConfig _config = _config;
    private readonly IPostRecoveryCommandService _postRecoveryCommandService = postRecoveryCommandService;
    private readonly IPostTweetDetailRequestFactory _tweetDetailRequestFactory =
        tweetDetailRequestFactory;
    private readonly IMediaLogger _mediaLogger = _mediaLogger;
    private readonly IPostDownloader _downloader = _downloader;
    private readonly IPostDomainParser _parser = _parser;

    public async Task Recovery(IPostDomainData postData, UsersContext context)
    {
        using CancellationTokenSource tokenSource = new();

        await _postRecoveryCommandService.Execute(
            new PostRecoveryCommand(
                _logger,
                _config,
                _mediaLogger,
                _downloader,
                _parser,
                _tweetDetailRequestFactory,
                postData,
                context
            ),
            tokenSource.Token
        );
    }

    private sealed class PostRecoveryCommand(
        ILogger<PostRecovery> logger,
        AppConfig config,
        IMediaLogger mediaLogger,
        IPostDownloader downloader,
        IPostDomainParser parser,
        IPostTweetDetailRequestFactory tweetDetailRequestFactory,
        IPostDomainData postData,
        UsersContext context
    ) : IPostRecoveryCommand
    {
        public IPostRecoverySession CreateSession() =>
            new RecoverySession(
                logger,
                config,
                mediaLogger,
                downloader,
                parser,
                tweetDetailRequestFactory,
                context
            );

        public async Task MergeRecoveredPosts(IReadOnlyCollection<Backup.Domain.Posts.Post> posts)
        {
            await postData.AddPosts(context.UserId, RecoveryOrigin, [.. posts], new() { Index = false });
        }

        public async Task SavePosts()
        {
            logger.LogInformation("saving posts");
            await postData.Save();
        }

        public void OnNoPostsRecovered() => logger.LogInformation("recovery has no posts to merge");

        public void OnPostsMerged(int count) => logger.LogInformation("post {post} merged", count);

        public void OnError(Exception exception) =>
            logger.LogError("Error: {error}", JsonConvert.SerializeObject(exception));
    }

    private sealed class RecoverySession(
        ILogger<PostRecovery> logger,
        AppConfig config,
        IMediaLogger mediaLogger,
        IPostDownloader downloader,
        IPostDomainParser parser,
        IPostTweetDetailRequestFactory tweetDetailRequestFactory,
        UsersContext context
    ) : IPostRecoverySession
    {
        private readonly ILogger<PostRecovery> _logger = logger;
        private readonly AppConfig _config = config;
        private readonly IMediaLogger _mediaLogger = mediaLogger;
        private readonly IPostDownloader _downloader = downloader;
        private readonly IPostDomainParser _parser = parser;
        private readonly UsersContext _context = context;
        private readonly Request? _request = tweetDetailRequestFactory.Build(context);
        private List<LogEntry>? _errorLogs;

        public bool RecoveryEnabled => _config.Services.Recovery.Enabled;

        public int MaxRecoveryPosts => 10;

        public bool CanDownloadTweetDetail => _request is not null;

        public async Task<IReadOnlyCollection<PostRecoveryLog>> GetRecoveryLogs()
        {
            _errorLogs = await _mediaLogger.GetErrors();

            if (_errorLogs is null)
            {
                _logger.LogInformation("no posts errores");
                return [];
            }

            return _errorLogs
                .Select(log => new PostRecoveryLog
                {
                    PostId = log.Id,
                    Messages = log.Messages.Select(message => message.Message).ToList(),
                })
                .ToList();
        }

        public async Task<Backup.Domain.Posts.Post?> DownloadPost(
            string postId,
            CancellationToken cancellationToken
        )
        {
            if (_request is null)
                return null;

            _request.Query.Variables["focalTweetId"] = postId;

            string response = await _downloader.Download(_request, cancellationToken);
            ParseResult result = _parser.Parse(_context.UserId, RecoveryOrigin, response);

            return result.Posts.Count == 0 ? null : result.Posts[0];
        }

        public async Task MarkRecovered(string postId)
        {
            if (_errorLogs is null)
                return;

            List<LogEntry> matchedLogs = [.. _errorLogs.Where(entry => entry.Id == postId)];

            if (matchedLogs.Count == 0)
                return;

            await _mediaLogger.RemoveErrors(matchedLogs);
            _errorLogs.RemoveAll(entry => entry.Id == postId);
        }

        public async Task DelayBetweenDownloads(CancellationToken cancellationToken) =>
            await Task.Delay(5 * 1000, cancellationToken);

        public void OnRecoveryDisabled() => _logger.LogInformation("post recovery disabled");

        public void OnSelectedPosts(int count) =>
            _logger.LogInformation("{count} posts with errors", count);

        public void OnTweetDetailUnavailable() =>
            _logger.LogWarning("api 'TweetDetail' is disabled or not configured");

        public void OnRecoveredPost(string postId) =>
            _logger.LogInformation("post {post} downloaded", postId);
    }
}
