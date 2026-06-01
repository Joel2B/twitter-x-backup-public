using Backup.Application.Posts.Models;
using Backup.Domain.Posts;

namespace Backup.Application.Posts;

public interface IPostChangeReadModelProjectionService
{
    IReadOnlyList<Change> Project(Post currentPost, IReadOnlyList<PostChangeReplayEntry> entries);
}
