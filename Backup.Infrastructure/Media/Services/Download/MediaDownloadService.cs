using Backup.Application.IO;
using Backup.Application.Media;
using Backup.Application.Media.Models;
using Backup.Application.Media.Ports;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Models;
using Backup.Infrastructure.Media.Models.Logging;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Proxy.Abstractions.Core;
using Backup.Infrastructure.Proxy.Services;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Services;

class MediaDownloadService(
    ILogger<MediaDownloadService> _logger,
    AppConfig _config,
    IMediaParallelDownloadPolicyService mediaParallelDownloadPolicyService,
    IMediaDownloadQueueBuilderService mediaDownloadQueueBuilderService,
    IMediaDownloadExecutionService mediaDownloadExecutionService,
    IMediaDownloadParallelRunner mediaDownloadParallelRunner,
    IDataStoreGuardService dataStoreGuardService,
    IMediaDownloader _downloader,
    IMediaLogger _mediaLogger,
    IProxyProvider proxyProvider
) : IMediaDownloadService, IMediaDownloadExecutionCommand
{
    private readonly ILogger<MediaDownloadService> _logger = _logger;
    private readonly AppConfig _config = _config;
    private readonly IMediaParallelDownloadPolicyService _mediaParallelDownloadPolicyService =
        mediaParallelDownloadPolicyService;
    private readonly IMediaDownloadQueueBuilderService _mediaDownloadQueueBuilderService =
        mediaDownloadQueueBuilderService;
    private readonly IMediaDownloadExecutionService _mediaDownloadExecutionService =
        mediaDownloadExecutionService;
    private readonly IMediaDownloadParallelRunner _mediaDownloadParallelRunner =
        mediaDownloadParallelRunner;
    private readonly IDataStoreGuardService _dataStoreGuardService = dataStoreGuardService;
    private readonly IMediaDownloader _downloader = _downloader;
    private readonly IMediaLogger _mediaLogger = _mediaLogger;
    private readonly IProxyProvider _proxyProvider = proxyProvider;

    private IMediaStorage? _mediaData;
    private IMediaStorage Data =>
        _dataStoreGuardService.RequireInitialized(_mediaData, "media data not initialized");

    public async Task Download(
        List<Download> downloads,
        IMediaStorage data,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_config.Downloads.Enabled)
            return;

        _mediaData = data;

        MediaParallelDownloadSettings settings = _mediaParallelDownloadPolicyService.Create(
            _config.Downloads.Threads.Min,
            _config.Downloads.Threads.Max,
            _config.Downloads.Threads.Start
        );

        IReadOnlyList<MediaDownloadQueueItem> queue = _mediaDownloadQueueBuilderService.Build(
            downloads.SelectMany(download =>
                download.Data.Select(data => new MediaDownloadQueueItem
                {
                    DownloadId = download.Id,
                    Url = data.Url,
                    Path = data.Path,
                })
            ),
            _config.Downloads.Count
        );

        _logger.LogInformation("media downloads queued: {count}", queue.Count);

        await _mediaDownloadExecutionService.Run(
            this,
            _mediaDownloadParallelRunner,
            queue,
            settings,
            cancellationToken
        );
    }

    public async Task<Stream> Download(
        MediaDownloadQueueItem item,
        CancellationToken cancellationToken
    )
    {
        DataDownload data = new() { Url = item.Url, Path = item.Path };
        return await _downloader.Download(data, Data, cancellationToken);
    }

    public Task Save(
        MediaDownloadQueueItem item,
        Stream stream,
        CancellationToken cancellationToken
    ) => Data.Save(stream, item.Path, cancellationToken);

    public void OnSuccess(MediaDownloadQueueItem item)
    {
        Logs logs = new()
        {
            Id = item.DownloadId,
            Messages = [new() { Id = item.Url, Message = item.Path }],
        };

        _mediaLogger.Log(logs);
    }

    public void OnItemError(MediaDownloadQueueItem item, string message)
    {
        Logs logs = new()
        {
            Id = item.DownloadId,
            Messages = [new() { Id = item.Url, Message = message }],
        };

        _mediaLogger.Error(logs);
    }

    public bool ShouldCancelOnItemError(Exception exception) => exception is ProxyException;

    public void OnFatalError(string message) => _logger.LogError("Error: {error}", message);

    public void OnDebug(string message) => _logger.LogInformation("{msg}", message);

    public Task SaveState() => _proxyProvider.SaveData();

    public Task SaveLogs()
    {
        _logger.LogInformation("saving logs");
        return _mediaLogger.Save();
    }
}
