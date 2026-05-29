using Backup.Infrastructure.Interfaces.Data.Posts;
using Backup.Infrastructure.Models.Config.Api;

namespace Backup.Infrastructure.Interfaces.Services.Posts;

public interface IPostRecovery
{
    public Task Recovery(IPostData postData, UsersContext context);
}

