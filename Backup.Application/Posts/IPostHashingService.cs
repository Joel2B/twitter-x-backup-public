using Backup.Domain.Posts;

namespace Backup.Application.Posts;

public interface IPostHashingService
{
    string Compute(Post post);
}
