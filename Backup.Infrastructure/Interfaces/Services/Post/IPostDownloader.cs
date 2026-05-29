using Backup.Infrastructure.Models.Config.ApiRequest;

namespace Backup.Infrastructure.Interfaces.Services.Posts;

public interface IPostDownloader
{
    public Task<string> Download(Request request, CancellationToken token);
    public Task<bool> Verify();
}

