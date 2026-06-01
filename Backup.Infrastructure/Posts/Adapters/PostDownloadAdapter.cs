using Backup.Application.Posts;
using Backup.Infrastructure.Dump.Abstractions.Data;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Posts.Abstractions.Data;
using Backup.Infrastructure.Posts.Abstractions.Services;
using Backup.Infrastructure.Posts.Models;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Posts.Adapters;

public class PostDownload(
    ILogger<PostDownload> _logger,
    IPostDownloadCommandService postDownloadCommandService,
    IPostDownloader _downloader,
    IPostLogger _postLogger,
    IPostDomainParser _parser,
    IDumpData _dump
) : IPostDownload
{
    private readonly ILogger<PostDownload> _logger = _logger;
    private readonly IPostDownloadCommandService _postDownloadCommandService =
        postDownloadCommandService;
    private readonly IPostDownloader _downloader = _downloader;
    private readonly IPostLogger _postLogger = _postLogger;
    private readonly IPostDomainParser _parser = _parser;
    private readonly IDumpData _dump = _dump;

    public async Task Download(IPostDomainData postData, ApiContext context)
    {
        using CancellationTokenSource tokenSource = new();

        await _postDownloadCommandService.Execute(
            new PostDownloadCommandAdapter(
                _logger,
                _downloader,
                _postLogger,
                _parser,
                _dump,
                postData,
                context
            ),
            tokenSource.Token
        );
    }
}
