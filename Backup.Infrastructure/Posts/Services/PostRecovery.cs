using Backup.Application.Posts;
using Backup.Infrastructure.Interfaces.Data.Posts;
using Backup.Infrastructure.Interfaces.Services.Media;
using Backup.Infrastructure.Interfaces.Services.Posts;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Posts.Adapters;
using Microsoft.Extensions.Logging;

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
