using Backup.Infrastructure.Models.Config.Api;

namespace Backup.Infrastructure.Interfaces.Services.Posts;

public interface IPostService
{
    public Task Recover(UsersContext context);
    public Task Download(ApiContext context);
}

