using System.Diagnostics;
using System.Net;
using Backup.Application.Media;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Models;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Proxy.Abstractions.Core;
using Backup.Infrastructure.Utility.Abstractions.Services;
using Backup.Infrastructure.Utils;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Services;

public class MediaDownloaderHttp(
    ILogger<MediaDownloaderHttp> _logger,
    AppConfig _config,
    IMediaDownloadExceptionPolicyService mediaDownloadExceptionPolicyService,
    IMediaDownloadPolicyService mediaDownloadPolicyService,
    IMediaDownloadContentValidationPolicyService mediaDownloadContentValidationPolicyService,
    IMediaDownloadStreamingPolicyService mediaDownloadStreamingPolicyService,
    IMediaDownloadProgressPolicyService mediaDownloadProgressPolicyService,
    IProxyProvider proxyProvider,
    IBandwidthLimiter bandwidthLimiter
) : IMediaDownloader
{
    private readonly ILogger<MediaDownloaderHttp> _logger = _logger;
    private readonly AppConfig _config = _config;
    private readonly IMediaDownloadExceptionPolicyService _mediaDownloadExceptionPolicyService =
        mediaDownloadExceptionPolicyService;
    private readonly IMediaDownloadPolicyService _mediaDownloadPolicyService =
        mediaDownloadPolicyService;
    private readonly IMediaDownloadContentValidationPolicyService _mediaDownloadContentValidationPolicyService =
        mediaDownloadContentValidationPolicyService;
    private readonly IMediaDownloadStreamingPolicyService _mediaDownloadStreamingPolicyService =
        mediaDownloadStreamingPolicyService;
    private readonly IMediaDownloadProgressPolicyService _mediaDownloadProgressPolicyService =
        mediaDownloadProgressPolicyService;

    private readonly IProxyProvider _proxy = proxyProvider;
    private readonly IBandwidthLimiter _bandwidthLimiter = bandwidthLimiter;

    public async Task<Stream> Download(
        DataDownload data,
        IMediaStorage mediaData,
        CancellationToken token
    )
    {
        while (true)
        {
            Stream? stream = null;

            try
            {
                HttpClient client = _proxy.GetClient();

                using HttpRequestMessage requestHttp = new(HttpMethod.Get, data.Url);

                Stopwatch sw = Stopwatch.StartNew();

                using HttpResponseMessage response = await client.SendAsync(
                    requestHttp,
                    HttpCompletionOption.ResponseHeadersRead,
                    token
                );

                long headersMs = sw.ElapsedMilliseconds;

                HttpStatusCode code = response.StatusCode;
                _mediaDownloadContentValidationPolicyService.EnsureSuccessStatusCode(code);

                long? contentLength = response.Content.Headers.ContentLength;

                _mediaDownloadPolicyService.EnsureAllowedContentLength(
                    contentLength,
                    _config.Downloads.MaxBytes,
                    UtilsStorage.FormatBytes
                );

                await using Stream content = await response.Content.ReadAsStreamAsync(token);
                _mediaDownloadContentValidationPolicyService.EnsureReadable(content);

                long inMemoryThreshold = int.MaxValue;
                long knownContentLength = contentLength ?? -1;

                if (
                    contentLength is long length
                    && _mediaDownloadPolicyService.ShouldUseMemoryStream(
                        contentLength,
                        inMemoryThreshold
                    )
                )
                    stream = new MemoryStream((int)length);
                else
                    stream = mediaData.GetTempStream();

                Backup.Application.Media.Models.MediaDownloadStreamingSettings streamingSettings =
                    _mediaDownloadStreamingPolicyService.GetSettings();
                byte[] buffer = new byte[streamingSettings.BufferSizeBytes];
                long totalRead = 0;
                int read;
                int nextPercent = streamingSettings.ProgressStepPercent;

                async Task<int> Read()
                {
                    using CancellationTokenSource cts =
                        CancellationTokenSource.CreateLinkedTokenSource(token);

                    Task<int> readTask = content
                        .ReadAsync(buffer.AsMemory(0, streamingSettings.BufferSizeBytes), cts.Token)
                        .AsTask();

                    Task delayTask = Task.Delay(_config.Downloads.NoDataTimeout, token);
                    Task completed = await Task.WhenAny(readTask, delayTask);

                    if (completed != readTask)
                    {
                        cts.Cancel();

                        throw new TaskCanceledException(
                            _mediaDownloadStreamingPolicyService.BuildNoDataTimeoutMessage(
                                _config.Downloads.NoDataTimeout
                            )
                        );
                    }

                    return await readTask;
                }

                if (
                    knownContentLength >= 0
                    && _mediaDownloadPolicyService.ShouldReportProgress(
                        contentLength,
                        streamingSettings.ProgressThresholdBytes
                    )
                )
                    while ((read = await Read()) > 0)
                    {
                        await _bandwidthLimiter.Throttle(read, token);

                        await stream.WriteAsync(buffer.AsMemory(0, read), token);
                        totalRead += read;

                        int percent = _mediaDownloadProgressPolicyService.CalculatePercent(
                            totalRead,
                            knownContentLength
                        );

                        if (
                            _mediaDownloadProgressPolicyService.ShouldEmitProgressLog(
                                percent,
                                nextPercent
                            )
                        )
                        {
                            _logger.LogInformation(
                                "{url}, {path}: {percent}% ({bytes}/{total})",
                                data.Url,
                                data.Path,
                                percent,
                                UtilsStorage.FormatBytes(totalRead),
                                UtilsStorage.FormatBytes(knownContentLength)
                            );

                            nextPercent = _mediaDownloadProgressPolicyService.GetNextThreshold(
                                nextPercent,
                                streamingSettings.ProgressStepPercent
                            );
                        }
                    }
                else
                    while ((read = await Read()) > 0)
                    {
                        await _bandwidthLimiter.Throttle(read, token);
                        await stream.WriteAsync(buffer.AsMemory(0, read), token);

                        totalRead += read;
                    }

                _mediaDownloadContentValidationPolicyService.EnsureNotEmpty(stream.Length);
                _mediaDownloadContentValidationPolicyService.EnsureComplete(
                    contentLength,
                    stream.Length
                );

                stream.Position = 0;

                long totalMs = sw.ElapsedMilliseconds;
                long downloadMs = totalMs - headersMs;

                if (_mediaDownloadPolicyService.ShouldLogTiming(_config.Downloads.Threads.Start))
                    _logger.LogInformation(
                        "TTFB: {ttfb} ms, Download: {download} ms, Total: {total} ms, Size: {bytes} Bytes ({kib} KiB)",
                        headersMs,
                        downloadMs,
                        totalMs,
                        stream.Length,
                        Math.Round(stream.Length / 1024m, 2, MidpointRounding.ToZero)
                    );

                await _proxy.Reset();
                _proxy.OnUse();

                return stream;
            }
            catch (Exception ex)
                when (_mediaDownloadExceptionPolicyService.ShouldRetryWithNextProxy(ex))
            {
                stream?.Dispose();

                _logger.LogError(
                    "Error in {url} ({type}): {error}",
                    data.Url,
                    ex.GetType().Name,
                    ex.Message
                );

                _proxy.OnError(ex);
                await _proxy.Next(token);
            }
            catch (SystemException)
            {
                stream?.Dispose();
                throw;
            }
            catch (Exception ex)
            {
                stream?.Dispose();
                _logger.LogError("Error 3: {error}", ex.Message);
                throw;
            }
        }
    }
}
