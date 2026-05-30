using System.Collections.Concurrent;
using System.Text;
using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Data;
using Backup.Infrastructure.Models.Partition;
using Backup.Infrastructure.Utils;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Data.Partition;

public class LocalPartition(
    AppConfig _appConfig,
    ILogger<LocalPartition> _logger,
    Storage? _config = null
) : IPartition, ISetup
{
    private readonly ILogger<LocalPartition> _logger = _logger;
    private readonly Storage? _config = _config;
    private readonly AppConfig _appConfig = _appConfig;

    private readonly ConcurrentDictionary<int, PartitionSize> _partitions = new(
        _appConfig
            .Data.Partitions.ToDictionary(o => o.Id)
            .ToDictionary(o => o.Key, o => new PartitionSize { Size = 0, Partition = o.Value })
    );

    public Task Setup()
    {
        PrintAliases();
        Print();
        SetupPaths();
        Print();
        Check();

        return Task.CompletedTask;
    }

    private void PrintAliases()
    {
        _logger.LogInformation("{alias,-5} {value,-5}", "Alias", "Value");

        foreach (var kvp in _appConfig.Data.Aliases)
            _logger.LogInformation("{alias,-5} {value,-5}", kvp.Key, kvp.Value);
    }

    private void SetupPaths()
    {
        foreach (PartitionConfig partition in _appConfig.Data.Partitions)
            partition.Paths = [UtilsPath.GetPartitionPath(_appConfig, partition)];
    }

    private void Print()
    {
        _logger.LogInformation(
            "{id,-5} {enabled,-5} {partition,-9} {size,-10} {path}",
            "Id",
            "Enabled",
            "Partition",
            "Size",
            "Path"
        );

        foreach (var kvp in _partitions)
        {
            _logger.LogInformation(
                "{id,-5} {enabled,-5} {partition,-9} {size,-10} {path}",
                kvp.Value.Partition.Id,
                kvp.Value.Partition.Enabled,
                kvp.Value.Partition.Name,
                UtilsStorage.FormatBytes(kvp.Value.Size),
                Path.Combine([.. kvp.Value.Partition.Paths])
            );
        }
    }

    private void Check()
    {
        _logger.LogInformation("{title}", "Testing paths");
        _logger.LogInformation("{partition,-9} {error}", "Partition", "Error");

        bool stop = false;

        foreach (var kvp in _partitions)
        {
            if (!kvp.Value.Partition.Enabled)
                continue;

            string fileName = $"{Guid.NewGuid():N}";
            string path = Path.Combine([.. kvp.Value.Partition.Paths, fileName]);
            string? error = CheckPath(path);

            _logger.LogInformation("{partition,-9} {error}", kvp.Value.Partition.Name, error);

            if (error is not null)
                stop = true;
        }

        if (stop)
            throw new IOException();
    }

    private static string? CheckPath(string path)
    {
        try
        {
            using FileStream fs = new(
                path,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 16 * 1024,
                options: FileOptions.WriteThrough
            );

            byte[] payload = Encoding.UTF8.GetBytes("ok");
            fs.Write(payload, 0, payload.Length);
            fs.Flush(true);
        }
        catch (Exception ex)
        {
            return ex.Message;
        }

        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            return ex.Message;
        }

        return null;
    }

    public void SetupSizes(Dictionary<int, long> sizes)
    {
        foreach (var kvp in sizes)
        {
            if (!_partitions.ContainsKey(kvp.Key))
                continue;

            _partitions[kvp.Key].Size = kvp.Value;
        }

        Print();
    }

    public List<PartitionConfig> GetPartitions(List<int>? ids = null)
    {
        List<int>? selectedIds = ids ?? _config?.Partitions;
        HashSet<int>? idSet = selectedIds is null ? null : [.. selectedIds];

        return
        [
            .. _appConfig.Data.Partitions.Where(o =>
                o.Enabled && (idSet is null || idSet.Contains(o.Id))
            ),
        ];
    }

    public PartitionConfig GetPath(int? id = null, long size = 0)
    {
        int? idIndex;

        if (id is null)
        {
            if (size == 0)
                idIndex = GetPrimary().Id;
            else
            {
                PartitionConfig? partitionAvailable = CheckSpace(size);

                if (partitionAvailable is null)
                    throw new Exception("no space available");

                idIndex = partitionAvailable.Id;
            }
        }
        else
            idIndex = id;

        if (idIndex is null)
            throw new Exception($"not exist partition id {idIndex}");

        return _partitions[(int)idIndex].Partition;
    }

    private PartitionConfig? CheckSpace(long size)
    {
        PartitionConfig primary = GetPartitions().First(o => o.Type == "primary");
        List<PartitionConfig> extensions = [.. GetPartitions().Where(o => o.Type == "extension")];
        List<PartitionConfig> partitions = [primary, .. extensions];

        foreach (PartitionConfig partition in partitions)
        {
            long usableSize = 1_000_000_000L * partition.UsableSpace / 100 * partition.Size;

            if ((_partitions[partition.Id].Size + size) > usableSize)
                continue;

            _partitions[partition.Id].Add(size);

            return partition;
        }

        return null;
    }

    public List<PartitionConfig> GetCache() =>
        [
            .. GetPartitions()
                .Where(o => o.Type == "cache" || (o.Tags is not null && o.Tags.Contains("cache"))),
        ];

    public PartitionConfig GetPrimary() => GetPartitions().First(o => o.Type == "primary");

    public PartitionConfig GetHeavy() => GetPartitions().First(o => o.Type == "heavy");
}
