namespace Backup.Application.Posts;

public interface IPostProfileCountAggregationService
{
    IReadOnlyDictionary<string, int> CountByProfileIds(
        IEnumerable<string> profileIds,
        IReadOnlySet<string> filter
    );
}
