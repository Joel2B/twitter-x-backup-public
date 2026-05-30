using Backup.Infrastructure.Posts.Abstractions.Data;

namespace Backup.Infrastructure.Posts.Abstractions.Services;

public interface IPostReplication
{
    public Task Replicate(IEnumerable<IPostDataStore> stores);
}
