using Backup.Application.Posts.Ports;
using Backup.Domain.Posts;

namespace Backup.Application.Posts;

public interface IPostStoreParityService
{
    Task<PostStoreParityResult> Verify(IEnumerable<IPostStoreCountSource> stores);
}
