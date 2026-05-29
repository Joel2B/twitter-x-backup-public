using Backup.Application.Posts.Models;
using Backup.Application.Posts.Ports;

namespace Backup.Application.Posts;

public interface IPostStoreParityService
{
    Task<PostStoreParityResult> Verify(IEnumerable<IPostStoreCountSource> stores);
}

