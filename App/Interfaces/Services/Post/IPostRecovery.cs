using Backup.App.Interfaces.Data.Post;
using Backup.App.Models.Config.Api;

namespace Backup.App.Interfaces.Services.Post;

public interface IPostRecovery
{
    public Task Recovery(IPostData postData, UsersContext context);
}
