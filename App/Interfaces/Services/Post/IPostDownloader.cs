using Backup.App.Models.Config.Request;

namespace Backup.App.Interfaces.Services.Post;

public interface IPostDownloader
{
    public Task<string> Download(Request request, CancellationToken token);
    public Task<bool> Verify();
}
