using Backup.Application.Posts.Ports;

namespace Backup.Application.Posts;

public interface IPostReplicationService
{
    Task Replicate(IEnumerable<IPostReplicationStore> stores);
}

