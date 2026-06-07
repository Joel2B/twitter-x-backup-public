using Backup.Infrastructure.Posts.Adapters;
using Backup.Infrastructure.Posts.Models.Stored;

namespace Backup.Infrastructure.Posts.Data.Postgres;

public partial class PostgresPostData
{
    private IReadOnlyList<Post> NormalizePosts(IReadOnlyCollection<Post> posts)
    {
        return PostSnapshotNormalizationAdapter.Normalize(_postSnapshotNormalizationService, posts);
    }
}
