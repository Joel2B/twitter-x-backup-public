using System.Diagnostics;
using Backup.App.Interfaces.Partition;
using Backup.App.Models.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;

namespace Backup.App.Extensions;

public static class LoggingCollectionExtensions
{
    public static IServiceCollection AddSerilog(this IServiceCollection services)
    {
        using ServiceProvider provider = services.BuildServiceProvider();
        Models.Config.App config = provider.GetRequiredService<Models.Config.App>();
        IPartition partitionService = provider.GetRequiredService<IPartition>();

        Models.Config.Data.Partition partition = partitionService
            .GetPartitions(config.Debug.Partitions)
            .First();

        string directory = Path.Combine(
            [.. partition.Paths, .. config.Debug.Paths, .. config.Debug.Log.Paths]
        );

        Directory.CreateDirectory(directory);

        string fileName = "app-.log";
        string path = Path.Combine(directory, fileName);

        string outputTemplate =
            "[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level:u3}] [{ShortSourceContext}]{ContextId} {Message:lj}{NewLine}";

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .Enrich.With<ShortSourceContextEnricher>()
            .WriteTo.Console(outputTemplate: outputTemplate)
            .WriteTo.File(
                path,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 10,
                outputTemplate: outputTemplate
            )
            .CreateLogger();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(dispose: true);
        });

        return services;
    }
}

public class ShortSourceContextEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (
            logEvent.Properties.TryGetValue("SourceContext", out var context)
            && context is ScalarValue sv
            && sv.Value is string full
        )
        {
            string shortName = full.Split('.').Last();

            LogEventProperty shortProp = propertyFactory.CreateProperty(
                "ShortSourceContext",
                shortName
            );

            logEvent.AddPropertyIfAbsent(shortProp);
        }
    }
}

public static class LoggingExtensions
{
    public static void LogInfo(
        this Microsoft.Extensions.Logging.ILogger logger,
        string? message,
        params object?[] args
    )
    {
        logger.LogInformation(message, [.. args]);
    }

    public static void LogInformation(
        this Microsoft.Extensions.Logging.ILogger logger,
        string? id,
        string? message,
        params object?[] args
    )
    {
        using (LogContext.PushProperty("ContextId", string.IsNullOrEmpty(id) ? "" : $" [{id}]"))
            logger.LogInformation(message, [.. args]);
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

        Diff diff = Utils.Text.Diff(json1, json2);

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
