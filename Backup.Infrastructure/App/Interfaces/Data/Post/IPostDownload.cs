using Backup.App.Interfaces.Data.Posts;
using Backup.App.Models.Config.Api;

namespace Backup.App.Models.Posts;

public interface IPostDownload
{
    public Task Download(IPostData postData, ApiContext context);
}
