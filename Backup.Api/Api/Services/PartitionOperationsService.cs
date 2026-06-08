using Backup.Api.Models;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Models.Config.Data;

namespace Backup.Api.Services;

public sealed class PartitionOperationsService(IPartition partition)
{
    private readonly IPartition _partition = partition;

    public IReadOnlyList<PartitionSummary> GetAll() =>
        _partition.GetPartitions().Select(MapPartition).ToList();

    public IReadOnlyList<PartitionSummary> GetCache() =>
        _partition.GetCache().Select(MapPartition).ToList();

    public PartitionSummary GetPrimary() => MapPartition(_partition.GetPrimary());

    public PartitionSummary GetHeavy() => MapPartition(_partition.GetHeavy());

    private static PartitionSummary MapPartition(PartitionConfig partition) =>
        new()
        {
            Id = partition.Id,
            Name = partition.Name,
            Type = partition.Type,
            Enabled = partition.Enabled,
            Size = partition.Size,
            UsableSpace = partition.UsableSpace,
            Paths = [.. partition.Paths],
            Tags = partition.Tags,
        };
}
