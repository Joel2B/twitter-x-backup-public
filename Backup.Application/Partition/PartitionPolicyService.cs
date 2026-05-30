using Backup.Application.Partition.Models;

namespace Backup.Application.Partition;

public sealed class PartitionPolicyService : IPartitionPolicyService
{
    public IReadOnlyList<PartitionState> FilterEnabled(
        IReadOnlyList<PartitionState> partitions,
        IReadOnlyCollection<int>? allowedIds
    )
    {
        HashSet<int>? filter = allowedIds is null ? null : [.. allowedIds];

        return partitions
            .Where(partition => partition.Enabled && (filter is null || filter.Contains(partition.Id)))
            .ToList();
    }

    public int GetRequiredPartitionIdByType(IReadOnlyList<PartitionState> partitions, string type)
    {
        PartitionState? match = partitions.FirstOrDefault(partition =>
            string.Equals(partition.Type, type, StringComparison.OrdinalIgnoreCase)
        );

        if (match is null)
            throw new InvalidOperationException($"partition type '{type}' not found");

        return match.Id;
    }

    public int ResolvePartitionId(IReadOnlyList<PartitionState> partitions, int? requestedId, long size)
    {
        if (requestedId is not null)
            return requestedId.Value;

        if (size <= 0)
            return GetRequiredPartitionIdByType(partitions, "primary");

        PartitionState? selected = SelectPartitionForSize(partitions, size);

        if (selected is null)
            throw new InvalidOperationException("no space available");

        return selected.Id;
    }

    public bool IsCachePartition(PartitionState partition) =>
        string.Equals(partition.Type, "cache", StringComparison.OrdinalIgnoreCase)
        || (partition.Tags is not null && partition.Tags.Contains("cache"));

    private static PartitionState? SelectPartitionForSize(
        IReadOnlyList<PartitionState> partitions,
        long size
    )
    {
        PartitionState? primary = partitions.FirstOrDefault(partition =>
            string.Equals(partition.Type, "primary", StringComparison.OrdinalIgnoreCase)
        );

        List<PartitionState> extensions = partitions
            .Where(partition =>
                string.Equals(partition.Type, "extension", StringComparison.OrdinalIgnoreCase)
            )
            .ToList();

        List<PartitionState> ordered = [];

        if (primary is not null)
            ordered.Add(primary);

        ordered.AddRange(extensions);

        foreach (PartitionState partition in ordered)
        {
            long usableSize = 1_000_000_000L * partition.UsableSpace / 100 * partition.Size;

            if ((partition.CurrentSize + size) <= usableSize)
                return partition;
        }

        return null;
    }
}
