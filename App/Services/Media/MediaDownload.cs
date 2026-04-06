using Backup.App.Interfaces.Proxy;
using Backup.App.Interfaces.Services.Media;
using Backup.App.Models.Media;
using Backup.App.Models.Media.Logging;
using Backup.App.Services.Proxy;
using Microsoft.Extensions.Logging;

namespace Backup.App.Services.Media;

class MediaDownload(
    ILogger<MediaDownload> _logger,
    Models.Config.App _config,
    IMediaDownloader _downloader,
    IMediaLogger _mediaLogger,
    IProxyProvider proxyProvider
) : IMediaDownload
{
    private readonly ILogger<MediaDownload> _logger = _logger;
    private readonly Models.Config.App _config = _config;
    private readonly IMediaDownloader _downloader = _downloader;
    private readonly IMediaLogger _mediaLogger = _mediaLogger;
    private readonly IProxyProvider _proxyProvider = proxyProvider;

    private IMediaData? _mediaData;
    private IMediaData Data => _mediaData ?? throw new Exception("media data not initialized");

    public async Task Download(List<Download> downloads, IMediaData data)
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

        DynamicParallelOptions options = new()
        {
            MinDegreeOfParallelism = _config.Downloads.Threads.Min,
            MaxDegreeOfParallelism = _config.Downloads.Threads.Max,
            StartDegreeOfParallelism = _config.Downloads.Threads.Start,
            TargetDuration = TimeSpan.FromSeconds(5),
            JumpToMaxOnFastAverage = false,
            EnableHeavyCut = true,
            HeavyThreshold = TimeSpan.FromSeconds(30),
            CancellationToken = cts.Token,
            EnableDebug = true,
            DebugSink = msg => _logger.LogInformation("{msg}", msg),
            StrictDecreaseGate = true,
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
