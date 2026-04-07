using Backup.App.Interfaces.Data.Post;

namespace Backup.App.Models.Post;

public interface IPostDownload
{
    public Task Download(IPostData postData);
}
