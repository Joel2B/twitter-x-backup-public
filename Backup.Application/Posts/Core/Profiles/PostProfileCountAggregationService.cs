namespace Backup.Application.Posts;

public sealed class PostProfileCountAggregationService : IPostProfileCountAggregationService
{
    public IReadOnlyDictionary<string, int> CountByProfileIds(
        IEnumerable<string> profileIds,
        IReadOnlySet<string> filter
    ) =>
        profileIds
            .Where(filter.Contains)
            .GroupBy(profileId => profileId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
}
