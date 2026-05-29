using Backup.Infrastructure.Models.Posts;

namespace Backup.Infrastructure.Interfaces.Data.Posts;

public interface IPostDataStore : IPostData
{
    public bool IsDefault { get; set; }
    public Task<PostStoreCounts> GetStoreCounts();
}

