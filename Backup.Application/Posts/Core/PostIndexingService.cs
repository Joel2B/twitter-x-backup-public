using Backup.Domain.Posts;

namespace Backup.Application.Posts;

public class PostIndexingService : IPostIndexingService
{
    public void ApplySequenceIndex(IReadOnlyList<Post> posts, string userId, string origin)
    {
        for (int i = 0; i < posts.Count; i++)
        {
            IndexData index = new()
            {
                Previous = i == 0 ? null : posts[i - 1].Id,
                Next = i == posts.Count - 1 ? null : posts[i + 1].Id,
            };

            posts[i].Index[userId] = [];
            posts[i].Index[userId][origin] = index;
        }
    }
}
