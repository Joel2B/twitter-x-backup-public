using System.Diagnostics;
using Backup.Infrastructure.Models.Utils;
using Newtonsoft.Json;
using Serilog.Context;

namespace Backup.Infrastructure.Logging;

public static class LoggingExtensions
{
    public static void LogInfo(
        this Microsoft.Extensions.Logging.ILogger logger,
        string? message,
        params object?[] args
    )
    {
        Microsoft.Extensions.Logging.LoggerExtensions.LogInformation(logger, message, args);
    }

    private static void LogInformation(
        this Microsoft.Extensions.Logging.ILogger logger,
        string? id,
        string? message,
        params object?[] args
    )
    {
        using (LogContext.PushProperty("ContextId", string.IsNullOrEmpty(id) ? "" : $" [{id}]"))
            Microsoft.Extensions.Logging.LoggerExtensions.LogInformation(logger, message, args);
    }

    public static void LogAsJsonDiff<T>(
        this Microsoft.Extensions.Logging.ILogger logger,
        string message1,
        string message2,
        T data1,
        T data2
    )
    {
        string json1 = JsonConvert.SerializeObject(data1, Formatting.Indented);
        string json2 = JsonConvert.SerializeObject(data2, Formatting.Indented);

        Diff diff = Backup.Infrastructure.Utils.Text.Diff(json1, json2);

        Microsoft.Extensions.Logging.LoggerExtensions.LogInformation(
            logger,
            "{message1}:",
            message1
        );

        foreach (string line in diff.Diff1)
            Microsoft.Extensions.Logging.LoggerExtensions.LogInformation(logger, "{line}", line);

        Microsoft.Extensions.Logging.LoggerExtensions.LogInformation(
            logger,
            "{message2}:",
            message2
        );

        foreach (string line in diff.Diff2)
            Microsoft.Extensions.Logging.LoggerExtensions.LogInformation(logger, "{line}", line);
    }

    public static IDisposable LogTimer(
        this Microsoft.Extensions.Logging.ILogger logger,
        string? id,
        string message
    )
    {
        logger.LogInformation(id, "{message}...", message);

        return new TimingScope(logger, id);
    }

    public static IDisposable LogTimer(
        this Microsoft.Extensions.Logging.ILogger logger,
        string message
    )
    {
        Microsoft.Extensions.Logging.LoggerExtensions.LogInformation(
            logger,
            "{message}...",
            message
        );

        return new TimingScope(logger);
    }

    private class TimingScope(Microsoft.Extensions.Logging.ILogger _logger, string? _id = null)
        : IDisposable
    {
        private readonly string? _id = _id;
        private readonly Microsoft.Extensions.Logging.ILogger _logger = _logger;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        public void Dispose()
        {
            _stopwatch.Stop();

            TimeSpan elapsed = _stopwatch.Elapsed;

            string elapsedFormatted =
                $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2}:{elapsed.Milliseconds:D3}";

            if (_id is null)
                Microsoft.Extensions.Logging.LoggerExtensions.LogInformation(
                    _logger,
                    "done in {elapsedFormatted}",
                    elapsedFormatted
                );
            else
                _logger.LogInformation(_id, "done in {elapsedFormatted}", elapsedFormatted);
        }
    }
}
