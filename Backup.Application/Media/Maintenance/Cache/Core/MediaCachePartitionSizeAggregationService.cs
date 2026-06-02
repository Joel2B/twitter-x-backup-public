namespace Backup.Application.Media.Maintenance;

public sealed class MediaCachePartitionSizeAggregationService
    : IMediaCachePartitionSizeAggregationService
{
    public IReadOnlyDictionary<int, long> Aggregate(
        IEnumerable<KeyValuePair<int?, long?>> partitionFileSizes
    )
    {
        Dictionary<int, long> result = [];

        foreach (KeyValuePair<int?, long?> item in partitionFileSizes)
        {
            int partitionId = item.Key ?? -1;
            long size = item.Value ?? 0;

            if (result.TryGetValue(partitionId, out long current))
                result[partitionId] = current + size;
            else
                result[partitionId] = size;
        }

        return result;
    }
}
