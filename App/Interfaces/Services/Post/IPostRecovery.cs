using Backup.App.Interfaces.Data.Posts;
using Backup.App.Models.Config.Api;

namespace Backup.App.Interfaces.Services.Posts;

public interface IPostRecovery
{
    public Task Recovery(IPostData postData, UsersContext context);
}
