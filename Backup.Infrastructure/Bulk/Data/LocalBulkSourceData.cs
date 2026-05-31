using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Infrastructure.Bulk.Abstractions.Data;
using Backup.Application.Bulk;
using Backup.Application.Bulk.Models;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Bulk.Models;
using Backup.Infrastructure.Models.Config.Data;
using Backup.Infrastructure.Models.Config.Data.Bulk;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Bulk.Data;

public class LocalBulkSourceData(
    ILogger<LocalBulkSourceData> _logger,
    StorageBulk _config,
    IPartition _partition,
    IBulkSourceExtractionService bulkSourceExtractionService,
    IBulkSourceReplicationPolicyService bulkSourceReplicationPolicyService
) : IBulkSourceDataStore, ISetup
{
    public bool IsDefault { get; set; }
    private readonly ILogger<LocalBulkSourceData> _logger = _logger;
    private readonly StorageBulk _config = _config;
    private readonly IPartition _partition = _partition;
    private readonly IBulkSourceExtractionService _bulkSourceExtractionService = bulkSourceExtractionService;
    private readonly IBulkSourceReplicationPolicyService _bulkSourceReplicationPolicyService =
        bulkSourceReplicationPolicyService;

    public Task Setup()
    {
        SetupDirectory();
        Replicate();

        return Task.CompletedTask;
    }

    private string GetPathSources(PartitionConfig? partition = null)
    {
        PartitionConfig primary = partition ?? _partition.GetPrimary();

        string path = Path.Combine(
            [.. primary.Paths, .. _config.Paths.Paths, .. _config.Paths.Sources.Paths]
        );

        return path;
    }

    private void SetupDirectory()
    {
        foreach (PartitionConfig partition in _partition.GetPartitions())
            Directory.CreateDirectory(GetPathSources(partition));
    }

    public async Task<List<Source>> GetSources()
    {
        string path = GetPathSources();
        string[] files = Directory.GetFiles(path);
        List<string> lines = [];
        foreach (string file in files)
            await foreach (string line in File.ReadLinesAsync(file))
                lines.Add(line);

        IReadOnlyList<BulkSourceLinkItem> extracted = _bulkSourceExtractionService.Extract(lines);
        return [.. extracted.Select(ToSource)];
    }

    private static Source ToSource(BulkSourceLinkItem item) =>
        new()
        {
            Link = item.Link,
            UserName = item.UserName,
            Type = item.Type switch
            {
                BulkSourceType.Media => SourceType.Media,
                BulkSourceType.Status => SourceType.Status,
                BulkSourceType.Notifications => SourceType.Notifications,
                _ => SourceType.None,
            },
        };

    private void Replicate()
    {
        List<PartitionConfig> partitions = _partition
            .GetPartitions()
            .Except([_partition.GetPrimary()])
            .ToList();

        string mainPath = GetPathSources();

        foreach (PartitionConfig partition in partitions)
        {
            string path = GetPathSources(partition);
            HashSet<string> replicaFileNames =
            [
                .. Directory
                    .EnumerateFiles(path)
                    .Select(file => Path.GetFileName(file))
                    .Where(fileName => !string.IsNullOrWhiteSpace(fileName)),
            ];

            IReadOnlyList<string> missingFileNames = _bulkSourceReplicationPolicyService.GetMissingFiles(
                Directory
                    .EnumerateFiles(mainPath)
                    .Select(file => Path.GetFileName(file))
                    .Where(fileName => !string.IsNullOrWhiteSpace(fileName)),
                replicaFileNames
            );

            foreach (string fileName in missingFileNames)
            {
                string sourceFile = Path.Combine(mainPath, fileName);
                string targetFile = Path.Combine(path, fileName);
                File.Copy(sourceFile, targetFile);
                _logger.LogInformation("{path} path copied", targetFile);
            }
        }
    }
}
