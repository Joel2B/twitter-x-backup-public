using Backup.App.Models.Config.ApiRequest;

namespace Backup.App.Interfaces.Services.Posts;

public interface IPostDownloader
{
    public Task<string> Download(Request request, CancellationToken token);
    public Task<bool> Verify();
}
