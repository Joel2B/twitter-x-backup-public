using Backup.App.Models.Config.Api;

namespace Backup.App.Interfaces.Services.Post;

public interface IPostService
{
    public Task Recover(string userId);
    public Task Download(ApiContext context);
}
