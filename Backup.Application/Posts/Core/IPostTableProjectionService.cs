using Backup.Application.Posts.Models;

namespace Backup.Application.Posts;

public interface IPostTableProjectionService
{
    PostTableProjectionResult Project(IReadOnlyList<Backup.Domain.Posts.Post> posts);
}
