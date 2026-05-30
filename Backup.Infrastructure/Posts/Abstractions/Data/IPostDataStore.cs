using Backup.Infrastructure.Interfaces.Data;
using Backup.Infrastructure.Models.Posts;

namespace Backup.Infrastructure.Posts.Abstractions.Data;

public interface IPostDataStore : IPostData, IDefaultStore
{
    public Task<PostStoreCounts> GetStoreCounts();
}
