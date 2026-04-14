using Backup.App.Interfaces.Data.Post;

namespace Backup.App.Interfaces.Services.Post;

public interface IPostRecovery
{
    public Task Recovery(IPostData postData, Models.Config.FetchContext fetchContext);
}
