using Backup.Infrastructure.DependencyInjection.Composition;
using Backup.Infrastructure.DependencyInjection.Runtime;
using Backup.Infrastructure.Hosting;
using Backup.Infrastructure.Media.Maintenance;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

bool automaticMediaPrune = args.Length >= 3 && args[0] == "maintenance" && args[1] == "media-prune";
bool maintenanceRequested = args.Length > 0 && args[0] == "maintenance";

if (maintenanceRequested && !automaticMediaPrune)
    throw new ArgumentException(
        "Unknown or incomplete maintenance command. Expected: maintenance media-prune <operation>."
    );

ServiceCollection services = new();
ConfigurationManager configuration = new();
configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
configuration.AddJsonFile(
    Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
    optional: true,
    reloadOnChange: false
);
configuration.AddEnvironmentVariables();

services.AddRuntimeConfiguration(configuration);
services.AddBackupCliInfrastructure();

Console.Error.WriteLine("[startup] building service provider");
await using ServiceProvider provider = services.BuildServiceProvider();

Console.Error.WriteLine("[startup] creating scope");
await using AsyncServiceScope scope = provider.CreateAsyncScope();

Console.Error.WriteLine("[startup] running setup");
await scope.ServiceProvider.RunBackupInfrastructureSetup();

Console.Error.WriteLine("[startup] resolving app");
if (automaticMediaPrune)
{
    await RunAutomaticMediaPrune(scope.ServiceProvider, args[2], args[3..]);
    return;
}

BackupCliRunner cliRunner = scope.ServiceProvider.GetRequiredService<BackupCliRunner>();
using CancellationTokenSource cts = new();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cts.Cancel();
    Console.Error.WriteLine("[shutdown] cancellation requested (Ctrl+C)");
};

Console.Error.WriteLine("[startup] running backup");
try
{
    await cliRunner.RunBackup(cts.Token);
}
catch (OperationCanceledException) when (cts.IsCancellationRequested)
{
    Console.Error.WriteLine("[shutdown] backup cancelled cleanly");
}
catch (Exception ex)
{
    scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("Program")
        .LogCritical(ex, "backup terminated unexpectedly");
    throw;
}

static async Task RunAutomaticMediaPrune(
    IServiceProvider provider,
    string operation,
    string[] options
)
{
    AutomaticLooseMediaPruneService service =
        provider.GetRequiredService<AutomaticLooseMediaPruneService>();
    string dataConfig = GetRequiredOption(options, "--data-config");
    string mediaStore = GetRequiredOption(options, "--media-store");
    string cache = GetRequiredOption(options, "--cache");
    int batchSize = int.Parse(GetOption(options, "--batch-size") ?? "1000");
    using CancellationTokenSource cts = new();
    void handler(object? _, ConsoleCancelEventArgs eventArgs)
    {
        eventArgs.Cancel = true;
        cts.Cancel();
    }
    Console.CancelKeyPress += handler;
    try
    {
        Progress<AutomaticLooseMediaPruneProgress> progress = new(value =>
        {
            Console.Error.WriteLine(
                $"[{value.Phase}] operations: {value.Completed}/{value.Total}, bytes: {value.Bytes}"
            );
            Console.Error.Flush();
        });
        if (operation is "preview" or "prune")
        {
            AutomaticLooseMediaPrunePreviewResult result =
                operation == "prune"
                    ? await service.PruneAsync(dataConfig, mediaStore, cache, progress, cts.Token)
                    : await service.PreviewAsync(
                        dataConfig,
                        mediaStore,
                        cache,
                        progress,
                        cts.Token
                    );
            Console.WriteLine($"run: {result.RunId}");
            Console.WriteLine($"state: {result.State}");
            Console.WriteLine($"approval: {result.ApprovalHash}");
            Console.WriteLine($"policy hash: {result.PolicyHash}");
            Console.WriteLine($"projection hash: {result.ProjectionHash}");
            Console.WriteLine($"operations: {result.Operations}");
            Console.WriteLine($"planned bytes: {result.PlannedBytes}");
            return;
        }
        if (operation == "apply")
        {
            AutomaticLooseMediaPruneResult result = await service.ApplyAsync(
                dataConfig,
                mediaStore,
                cache,
                Guid.Parse(GetRequiredOption(options, "--run-id")),
                GetRequiredOption(options, "--approval"),
                HasFlag(options, "--runtime-stopped-confirmed"),
                batchSize,
                progress,
                cts.Token
            );
            PrintResult(result, "bytes quarantined");
            Console.WriteLine($"purge after: {result.PurgeAfterUtc:O}");
            return;
        }
        if (operation == "status")
        {
            IReadOnlyList<AutomaticLooseMediaPruneStatus> statuses = await service.StatusAsync(
                dataConfig,
                mediaStore,
                cache,
                cts.Token
            );
            foreach (AutomaticLooseMediaPruneStatus status in statuses)
            {
                Console.WriteLine(
                    $"{status.RunId}  {status.State}  {status.Completed}/{status.Operations}  {status.Bytes}/{status.PlannedBytes} bytes  approval={status.ApprovalHash}  purge-after={status.PurgeAfterUtc:O}"
                );
            }
            return;
        }
        if (operation == "cancel")
        {
            Guid runId = Guid.Parse(GetRequiredOption(options, "--run-id"));
            await service.CancelAsync(dataConfig, mediaStore, cache, runId, cts.Token);
            Console.WriteLine($"run: {runId}");
            Console.WriteLine("state: cancelled");
            return;
        }
        if (operation == "verify")
        {
            AutomaticLooseMediaPruneResult result = await service.VerifyAsync(
                dataConfig,
                mediaStore,
                cache,
                Guid.Parse(GetRequiredOption(options, "--run-id")),
                GetRequiredOption(options, "--approval"),
                cts.Token
            );
            PrintResult(result, "bytes verified");
            return;
        }
        if (operation == "purge-expired")
        {
            AutomaticLooseMediaPurgeResult result = await service.PurgeExpiredAsync(
                dataConfig,
                mediaStore,
                cache,
                Guid.Parse(GetRequiredOption(options, "--run-id")),
                GetRequiredOption(options, "--approval"),
                HasFlag(options, "--runtime-stopped-confirmed"),
                batchSize,
                progress,
                cts.Token
            );
            Console.WriteLine($"files deleted: {result.FilesDeleted}");
            Console.WriteLine($"bytes deleted: {result.BytesDeleted}");
            return;
        }
        throw new ArgumentException($"Unknown media prune operation: {operation}");
    }
    finally
    {
        Console.CancelKeyPress -= handler;
    }
}

static void PrintResult(AutomaticLooseMediaPruneResult result, string bytesLabel)
{
    Console.WriteLine($"run: {result.RunId}");
    Console.WriteLine($"state: {result.State}");
    Console.WriteLine($"operations: {result.Completed}/{result.Operations}");
    Console.WriteLine($"{bytesLabel}: {result.BytesQuarantined}");
}

static string GetRequiredOption(string[] args, string name) =>
    GetOption(args, name) ?? throw new ArgumentException($"Missing required option {name}.");

static string? GetOption(string[] args, string name) => GetOptions(args, name).SingleOrDefault();

static List<string> GetOptions(string[] args, string name)
{
    List<string> values = [];
    for (int index = 0; index < args.Length; index++)
    {
        if (args[index] != name)
            continue;
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException($"Missing value for option {name}.");
        values.Add(args[++index]);
    }
    return values;
}

static bool HasFlag(string[] args, string name) => args.Contains(name, StringComparer.Ordinal);
