using Backup.Infrastructure.Core.Abstractions.Data;
using Backup.Infrastructure.Posts.Models.Stored;

namespace Backup.Infrastructure.Posts.Abstractions.Data;

public interface IPostDataStore : IPostData, IDefaultStore
{
    public Task<PostStoreCounts> GetStoreCounts();
}
