using Backup.App.Interfaces.Data.Post;

namespace Backup.App.Models.Post;

public interface IPostDownload
{
    public Task Download(Dictionary<string, Post> posts, IPostData postData);
}
