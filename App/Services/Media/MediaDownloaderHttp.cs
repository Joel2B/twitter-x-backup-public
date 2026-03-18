using System.Diagnostics;
using System.Net;
using Backup.App.Interfaces.Proxy;
using Backup.App.Interfaces.Services.Media;
using Backup.App.Interfaces.Services.UtilsService;
using Backup.App.Models.Media;
using Microsoft.Extensions.Logging;

namespace Backup.App.Services.Media;

public class MediaDownloaderHttp(
    ILogger<MediaDownloaderHttp> _logger,
    Models.Config.App _config,
    IProxyProvider proxyProvider,
    IBandwidthLimiter bandwidthLimiter
) : IMediaDownloader
{
    private readonly ILogger<MediaDownloaderHttp> _logger = _logger;
    private readonly Models.Config.App _config = _config;

    private readonly IProxyProvider _proxy = proxyProvider;
    private readonly IBandwidthLimiter _bandwidthLimiter = bandwidthLimiter;

    public async Task<Stream> Download(
        DataDownload data,
        IMediaData mediaData,
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

                if (code is not HttpStatusCode.OK)
                    throw new SystemException(code.ToString());

                long? contentLength = response.Content.Headers.ContentLength;

                if (
                    contentLength is not null
                    && _config.Downloads.MaxBytes > 0
                    && contentLength >= _config.Downloads.MaxBytes
                )
                    throw new SystemException(
                        $">= {Utils.Storage.FormatBytes(_config.Downloads.MaxBytes)}"
                    );

                await using Stream content = await response.Content.ReadAsStreamAsync(token);

                if (!content.CanRead)
                    throw new SystemException("content is empty.");

                long inMemoryThreshold = int.MaxValue;

                if (contentLength is not null && contentLength <= inMemoryThreshold)
                    stream = new MemoryStream((int)contentLength.Value);
                else
                    stream = mediaData.GetTempStream();

                const int BufferSize = 128 * 1024;

                bool progress = true;
                long progressThreshold = 10L * 1024 * 1024;
                byte[] buffer = new byte[BufferSize];
                long totalRead = 0;
                int read;
                const int StepPercent = 10;
                int nextPercent = StepPercent;

                async Task<int> Read()
                {
                    using CancellationTokenSource cts =
                        CancellationTokenSource.CreateLinkedTokenSource(token);

                    Task<int> readTask = content
                        .ReadAsync(buffer.AsMemory(0, BufferSize), cts.Token)
                        .AsTask();

                    Task delayTask = Task.Delay(_config.Downloads.NoDataTimeout, token);
                    Task completed = await Task.WhenAny(readTask, delayTask);

                    if (completed != readTask)
                    {
                        cts.Cancel();

                        throw new TaskCanceledException(
                            $"No data received in {_config.Downloads.NoDataTimeout} ms."
                        );
                    }

                    return await readTask;
                }

                if (progress && contentLength is not null && contentLength >= progressThreshold)
                    while ((read = await Read()) > 0)
                    {
                        await _bandwidthLimiter.Throttle(read, token);

                        await stream.WriteAsync(buffer.AsMemory(0, read), token);
                        totalRead += read;

                        int percent = (int)(totalRead * 100 / contentLength.Value);

                        if (percent >= nextPercent)
                        {
                            _logger.LogInformation(
                                "{url}, {path}: {percent}% ({bytes}/{total})",
                                data.Url,
                                data.Path,
                                percent,
                                Utils.Storage.FormatBytes(totalRead),
                                Utils.Storage.FormatBytes(contentLength.Value)
                            );

                            nextPercent += StepPercent;
                        }
                    }
                else
                    while ((read = await Read()) > 0)
                    {
                        await _bandwidthLimiter.Throttle(read, token);
                        await stream.WriteAsync(buffer.AsMemory(0, read), token);

                        totalRead += read;
                    }

                if (stream.Length == 0)
                    throw new SystemException("content is empty.");

                if (contentLength is not null && stream.Length != contentLength.Value)
                    throw new SystemException("incomplete download");

                stream.Position = 0;

                long totalMs = sw.ElapsedMilliseconds;
                long downloadMs = totalMs - headersMs;

                if (_config.Downloads.Threads.Start == 1)
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
            catch (Exception ex) when (ex is TaskCanceledException || ex is HttpRequestException)
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
