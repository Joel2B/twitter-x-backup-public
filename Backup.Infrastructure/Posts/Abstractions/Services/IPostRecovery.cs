using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Posts.Abstractions.Data;

namespace Backup.Infrastructure.Posts.Abstractions.Services;

public interface IPostRecovery
{
    public Task Recovery(IPostDomainData postData, UsersContext context);
}
