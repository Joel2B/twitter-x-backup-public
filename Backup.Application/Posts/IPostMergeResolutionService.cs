using Backup.Application.Posts.Models;
using Backup.Domain.Posts;

namespace Backup.Application.Posts;

public interface IPostMergeResolutionService
{
    IReadOnlyList<PostMergeResolutionItem> Resolve(
        string userId,
        string origin,
        IReadOnlyCollection<Post> incoming,
        IReadOnlyDictionary<string, Post> existing,
        MergeOptions options
    );
}
