using Backup.App.Models.Posts;

namespace Backup.App.Interfaces.Data.Posts;

public interface IPostDataStore : IPostData
{
    public bool IsDefault { get; set; }
    public Task<PostStoreCounts> GetStoreCounts();
}
