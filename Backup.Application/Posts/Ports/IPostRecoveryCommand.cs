using Backup.Domain.Posts;

namespace Backup.Application.Posts.Ports;

public interface IPostRecoveryCommand
{
    IPostRecoverySession CreateSession();
    Task MergeRecoveredPosts(IReadOnlyCollection<Post> posts);
    Task SavePosts();
    void OnNoPostsRecovered();
    void OnPostsMerged(int count);
    void OnError(Exception exception);
}
