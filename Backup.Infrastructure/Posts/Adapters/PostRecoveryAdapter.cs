using Backup.Application.Posts;
using Backup.Infrastructure.Posts.Abstractions.Data;
using Backup.Infrastructure.Interfaces.Services.Media;
using Backup.Infrastructure.Posts.Abstractions.Services;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Api;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Posts.Adapters;

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
            new PostRecoveryCommandAdapter(
                _logger,
                _config,
                _mediaLogger,
                _downloader,
                _parser,
                _tweetDetailRequestFactory,
                postData,
                context,
                RecoveryOrigin
            ),
            tokenSource.Token
        );
    }
}
