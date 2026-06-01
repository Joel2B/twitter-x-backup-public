using Backup.Application.IO;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Backup.Infrastructure.DependencyInjection.Base;

public static class StructuredLoggingInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddStructuredSerilogInfrastructure(
        this IServiceCollection services
    )
    {
        string outputTemplate =
            "[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level:u3}] [{ShortSourceContext}]{ContextId} {Message:lj}{NewLine}";

        LoggerConfiguration loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .Enrich.With<ShortSourceContextEnricher>()
            .WriteTo.Console(outputTemplate: outputTemplate);

        try
        {
            AppConfig config = GetAppConfig(services);
            int partitionId = config.Debug.Partitions.First();
            PartitionConfig partition = config.Data.Partitions.First(o => o.Id == partitionId);
            IReadOnlyList<string> resolvedPaths = PathAliasResolutionPolicy.ResolveAliases(
                partition.Paths,
                config.Data.Aliases
            );
            string basePath = PathCompositionPolicy.ComposePath(
                resolvedPaths,
                AppDomain.CurrentDomain.BaseDirectory
            );

            string directory = Path.Combine(
                [basePath, .. config.Debug.Paths, .. config.Debug.Log.Paths]
            );

            Directory.CreateDirectory(directory);

            string fileName = "app-.log";
            string path = Path.Combine(directory, fileName);

            loggerConfiguration = loggerConfiguration.WriteTo.File(
                path,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 10,
                outputTemplate: outputTemplate
            );

            Console.Error.WriteLine($"[logger] file sink enabled: {path}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"[logger] file sink disabled, using console only. reason: {ex.GetType().Name}: {ex.Message}"
            );
        }

        Log.Logger = loggerConfiguration.CreateLogger();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(dispose: true);
        });

        return services;
    }

    private static AppConfig GetAppConfig(IServiceCollection services)
    {
        ServiceDescriptor? descriptor = services.LastOrDefault(o =>
            o.ServiceType == typeof(AppConfig)
        );

        if (descriptor?.ImplementationInstance is AppConfig config)
        {
            return config;
        }

        throw new InvalidOperationException(
            $"{nameof(AppConfig)} is not registered as an implementation instance."
        );
    }
}

internal sealed class ShortSourceContextEnricher : ILogEventEnricher
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
