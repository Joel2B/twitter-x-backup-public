using Backup.Application.Posts.Models;

namespace Backup.Application.Posts;

public interface IPostTableMaterializationService
{
    IReadOnlyList<Backup.Domain.Posts.Post> Materialize(PostTableMaterializationInput input);
}
