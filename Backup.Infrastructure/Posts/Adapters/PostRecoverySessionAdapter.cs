using Backup.Application.Posts.Models;
using Backup.Application.Posts.Ports;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Posts.Abstractions.Services;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Config.Request;
using Backup.Infrastructure.Media.Models.Logging;
using Microsoft.Extensions.Logging;
using ParseResult = Backup.Domain.Posts.ParseResult;

namespace Backup.Infrastructure.Posts.Adapters;

public sealed class PostRecoverySessionAdapter(
    ILogger logger,
    AppConfig config,
    IMediaLogger mediaLogger,
    IPostDownloader downloader,
    IPostDomainParser parser,
    IPostTweetDetailRequestFactory tweetDetailRequestFactory,
    UsersContext context,
    string recoveryOrigin
) : IPostRecoverySession
{
    private readonly ILogger _logger = logger;
    private readonly AppConfig _config = config;
    private readonly IMediaLogger _mediaLogger = mediaLogger;
    private readonly IPostDownloader _downloader = downloader;
    private readonly IPostDomainParser _parser = parser;
    private readonly UsersContext _context = context;
    private readonly string _recoveryOrigin = recoveryOrigin;
    private readonly Request? _request = tweetDetailRequestFactory.Build(context);
    private List<Logs>? _errorLogs;

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
        ParseResult result = _parser.Parse(_context.UserId, _recoveryOrigin, response);

        return result.Posts.Count == 0 ? null : result.Posts[0];
    }

    public async Task MarkRecovered(string postId)
    {
        if (_errorLogs is null)
            return;

        List<Logs> matchedLogs = [.. _errorLogs.Where(entry => entry.Id == postId)];

        if (matchedLogs.Count == 0)
            return;

        await _mediaLogger.RemoveErrors(matchedLogs);
        _errorLogs.RemoveAll(entry => entry.Id == postId);
    }

    public async Task DelayBetweenDownloads(CancellationToken cancellationToken) =>
        await Task.Delay(5 * 1000, cancellationToken);

    public void OnRecoveryDisabled() => _logger.LogInformation("post recovery disabled");

    public void OnSelectedPosts(int count) => _logger.LogInformation("{count} posts with errors", count);

    public void OnTweetDetailUnavailable() =>
        _logger.LogWarning("api 'TweetDetail' is disabled or not configured");

    public void OnRecoveredPost(string postId) =>
        _logger.LogInformation("post {post} downloaded", postId);
}
