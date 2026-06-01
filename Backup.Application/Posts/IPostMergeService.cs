using Backup.Application.Posts.Models;
using Backup.Domain.Posts;

namespace Backup.Application.Posts;

public interface IPostMergeService
{
    PostMergeOutcome Merge(
        string userId,
        string origin,
        Post current,
        Post incoming,
        MergeOptions options
    );
}
