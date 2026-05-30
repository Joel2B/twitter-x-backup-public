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
using DomainPost = Backup.Domain.Posts.Post;
using ParseResult = Backup.Domain.Posts.ParseResult;

namespace Backup.Infrastructure.Services.Posts;

public class PostRecovery(
    ILogger<PostRecovery> _logger,
    AppConfig _config,
    IPostRecoveryOrchestrationService postRecoveryOrchestrationService,
    IMediaLogger _mediaLogger,
    IPostDownloader _downloader,
    IPostDomainParser _parser
) : IPostRecovery
{
    private const string RecoveryOrigin = "recovery";

    private readonly ILogger<PostRecovery> _logger = _logger;
    private readonly AppConfig _config = _config;
    private readonly IPostRecoveryOrchestrationService _postRecoveryOrchestrationService =
        postRecoveryOrchestrationService;
    private readonly IMediaLogger _mediaLogger = _mediaLogger;
    private readonly IPostDownloader _downloader = _downloader;
    private readonly IPostDomainParser _parser = _parser;

    public async Task Recovery(IPostDomainData postData, UsersContext context)
    {
        try
        {
            using CancellationTokenSource tokenSource = new();
            IReadOnlyCollection<DomainPost> posts = await _postRecoveryOrchestrationService.Recover(
                new RecoverySession(_logger, _config, _mediaLogger, _downloader, _parser, context),
                tokenSource.Token
            );

            if (posts.Count == 0)
            {
                _logger.LogInformation("recovery has no posts to merge");
                return;
            }

            await postData.AddPosts(context.UserId, RecoveryOrigin, [.. posts], new() { Index = false });
            _logger.LogInformation("post {post} merged", posts.Count);

            _logger.LogInformation("saving posts");
            await postData.Save();
        }
        catch (Exception ex)
        {
            _logger.LogError("Error: {error}", JsonConvert.SerializeObject(ex));
        }
    }

    private sealed class RecoverySession(
        ILogger<PostRecovery> logger,
        AppConfig config,
        IMediaLogger mediaLogger,
        IPostDownloader downloader,
        IPostDomainParser parser,
        UsersContext context
    ) : IPostRecoverySession
    {
        private readonly ILogger<PostRecovery> _logger = logger;
        private readonly AppConfig _config = config;
        private readonly IMediaLogger _mediaLogger = mediaLogger;
        private readonly IPostDownloader _downloader = downloader;
        private readonly IPostDomainParser _parser = parser;
        private readonly UsersContext _context = context;
        private readonly Request? _request = RequestMerge.Build(context.Api, "TweetDetail");
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

        public async Task<DomainPost?> DownloadPost(string postId, CancellationToken cancellationToken)
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

        public async Task DelayBetweenDownloads(CancellationToken cancellationToken)
            => await Task.Delay(5 * 1000, cancellationToken);

        public void OnRecoveryDisabled() => _logger.LogInformation("post recovery disabled");

        public void OnSelectedPosts(int count) => _logger.LogInformation("{count} posts with errors", count);

        public void OnTweetDetailUnavailable() =>
            _logger.LogWarning("api 'TweetDetail' is disabled or not configured");

        public void OnRecoveredPost(string postId) =>
            _logger.LogInformation("post {post} downloaded", postId);
    }
}


