using Backup.Domain.Posts;

namespace Backup.Application.Posts;

public interface IPostStoreCountsAggregationService
{
    PostStoreCounts Compute(IReadOnlyCollection<Post> posts, int hashMetaCount);
}
