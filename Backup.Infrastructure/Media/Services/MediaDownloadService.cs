using Backup.Infrastructure.Proxy.Abstractions.Core;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Application.Media;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Media.Models;
using Backup.Infrastructure.Media.Models.Logging;
using Backup.Infrastructure.Proxy.Services;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Services;

class MediaDownloadService(
    ILogger<MediaDownloadService> _logger,
    AppConfig _config,
    IMediaParallelDownloadPolicyService mediaParallelDownloadPolicyService,
    IMediaDownloader _downloader,
    IMediaLogger _mediaLogger,
    IProxyProvider proxyProvider
) : IMediaDownloadService
{
    private readonly ILogger<MediaDownloadService> _logger = _logger;
    private readonly AppConfig _config = _config;
    private readonly IMediaParallelDownloadPolicyService _mediaParallelDownloadPolicyService =
        mediaParallelDownloadPolicyService;
    private readonly IMediaDownloader _downloader = _downloader;
    private readonly IMediaLogger _mediaLogger = _mediaLogger;
    private readonly IProxyProvider _proxyProvider = proxyProvider;

    private IMediaStorage? _mediaData;
    private IMediaStorage Data => _mediaData ?? throw new Exception("media data not initialized");

    public async Task Download(List<Download> downloads, IMediaStorage data)
    {
        if (!_config.Downloads.Enabled)
            return;

        _mediaData = data;

        await ProcessDownloads(downloads);
        await SaveLogs();
    }

    private async Task ProcessDownloads(List<Download> downloads)
    {
        using CancellationTokenSource cts = new();
        Backup.Application.Media.Models.MediaParallelDownloadSettings settings =
            _mediaParallelDownloadPolicyService.Create(
                _config.Downloads.Threads.Min,
                _config.Downloads.Threads.Max,
                _config.Downloads.Threads.Start
            );

        DynamicParallelOptions options = new()
        {
            MinDegreeOfParallelism = settings.MinDegreeOfParallelism,
            MaxDegreeOfParallelism = settings.MaxDegreeOfParallelism,
            StartDegreeOfParallelism = settings.StartDegreeOfParallelism,
            TargetDuration = settings.TargetDuration,
            JumpToMaxOnFastAverage = settings.JumpToMaxOnFastAverage,
            EnableHeavyCut = settings.EnableHeavyCut,
            HeavyThreshold = settings.HeavyThreshold,
            CancellationToken = cts.Token,
            EnableDebug = settings.EnableDebug,
            DebugSink = msg => _logger.LogInformation("{msg}", msg),
            StrictDecreaseGate = settings.StrictDecreaseGate,
        };

        var data = downloads.SelectMany(download => download.Data.Select(data => (data, download)));

        HashSet<string> videoExtensions = new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".webm" };
        data = data.OrderBy(o => videoExtensions.Contains(Path.GetExtension(o.data.Path)) ? 1 : 0);

        if (_config.Downloads.Count >= 0)
            data = data.Take(_config.Downloads.Count);

        try
        {
            await DynamicParallel.ForEachAsync(
                data,
                options,
                async (data, token) =>
                {
                    await ProcessDownload(data.data, data.download, cts, token);
                }
            );
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError("Error: {error}", ex.Message);
        }
        finally
        {
            await _proxyProvider.SaveData();
        }
    }

    private async Task ProcessDownload(
        DataDownload data,
        Download download,
        CancellationTokenSource cts,
        CancellationToken token
    )
    {
        Logs logs = new()
        {
            Id = download.Id,
            Messages = [new() { Id = data.Url, Message = data.Path }],
        };

        try
        {
            using Stream stream = await _downloader.Download(data, Data, token);
            await Data.Save(stream, data.Path, token);

            _mediaLogger.Log(logs);
        }
        catch (ProxyException)
        {
            if (!cts.IsCancellationRequested)
                cts.Cancel();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logs.Messages[0].Message = ex.Message;
            _mediaLogger.Error(logs);
        }
    }

    private async Task SaveLogs()
    {
        _logger.LogInformation("saving logs");
        await _mediaLogger.Save();
    }
}
