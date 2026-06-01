using Backup.Application.Posts.Models;
using Backup.Domain.Posts;

namespace Backup.Application.Posts;

public interface IPostStoreMergeMutationService
{
    IReadOnlyList<PostStoreMergeMutation> BuildMergeMutations(
        string userId,
        string origin,
        IReadOnlyList<Post> incoming,
        IReadOnlyDictionary<string, Post> existing,
        MergeOptions options
    );
}
