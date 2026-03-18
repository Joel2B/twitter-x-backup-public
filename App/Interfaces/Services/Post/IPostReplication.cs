using Backup.App.Interfaces.Data.Post;

namespace Backup.App.Interfaces.Services.Post;

public interface IPostReplication
{
    public Task Replicate(IEnumerable<IPostData> data);
}
