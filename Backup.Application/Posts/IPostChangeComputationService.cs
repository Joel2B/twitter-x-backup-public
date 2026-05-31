using Backup.Application.Posts.Models;
using Backup.Domain.Posts;

namespace Backup.Application.Posts;

public interface IPostChangeComputationService
{
    IReadOnlyList<PostComputedChange> Compute(Post post);
}
