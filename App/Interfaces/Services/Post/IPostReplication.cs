using Backup.App.Interfaces.Data.Posts;

namespace Backup.App.Interfaces.Services.Posts;

public interface IPostReplication
{
    public Task Replicate(IEnumerable<IPostDataStore> stores);
}
