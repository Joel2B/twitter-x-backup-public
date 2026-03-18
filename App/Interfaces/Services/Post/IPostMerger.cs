using Backup.App.Models.Post;

namespace Backup.App.Interfaces.Services.Post;

public interface IPostMerger
{
    public void Merge(
        string userId,
        string origin,
        Dictionary<string, Models.Post.Post> posts,
        List<Models.Post.Post> result,
        MergeOptions? options = null
    );
}
