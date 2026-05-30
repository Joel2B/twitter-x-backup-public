using Backup.Infrastructure.Interfaces.Data;
using Backup.Infrastructure.Models.Posts;

namespace Backup.Infrastructure.Interfaces.Data.Posts;

public interface IPostDataStore : IPostData, IDefaultStore
{
    public Task<PostStoreCounts> GetStoreCounts();
}

