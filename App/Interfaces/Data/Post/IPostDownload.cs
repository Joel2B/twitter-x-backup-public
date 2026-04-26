using Backup.App.Interfaces.Data.Post;
using Backup.App.Models.Config.Api;

namespace Backup.App.Models.Post;

public interface IPostDownload
{
    public Task Download(IPostData postData, ApiContext context);
}
