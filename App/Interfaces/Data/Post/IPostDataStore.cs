namespace Backup.App.Interfaces.Data.Post;

public interface IPostDataStore : IPostData
{
    public bool IsDefault { get; set; }
    public Task<Models.Post.PostStoreCounts> GetStoreCounts();
}
