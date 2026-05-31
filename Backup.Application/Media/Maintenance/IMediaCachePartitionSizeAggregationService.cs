namespace Backup.Application.Media.Maintenance;

public interface IMediaCachePartitionSizeAggregationService
{
    IReadOnlyDictionary<int, long> Aggregate(
        IEnumerable<KeyValuePair<int?, long?>> partitionFileSizes
    );
}
