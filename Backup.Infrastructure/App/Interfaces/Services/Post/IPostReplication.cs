using Backup.Infrastructure.Interfaces.Data.Posts;

namespace Backup.Infrastructure.Interfaces.Services.Posts;

public interface IPostReplication
{
    public Task Replicate(IEnumerable<IPostDataStore> stores);
}

