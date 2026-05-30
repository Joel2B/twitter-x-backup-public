using Backup.Infrastructure.Posts.Abstractions.Data;
using Backup.Infrastructure.Models.Config.Api;

namespace Backup.Infrastructure.Posts.Abstractions.Services;

public interface IPostRecovery
{
    public Task Recovery(IPostDomainData postData, UsersContext context);
}
