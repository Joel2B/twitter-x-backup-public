using Backup.App.Models.Config.Api;

namespace Backup.App.Interfaces.Services.Posts;

public interface IPostService
{
    public Task Recover(UsersContext context);
    public Task Download(ApiContext context);
}
