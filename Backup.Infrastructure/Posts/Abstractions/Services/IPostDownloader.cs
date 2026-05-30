using Backup.Infrastructure.Models.Config.ApiRequest;

namespace Backup.Infrastructure.Posts.Abstractions.Services;

public interface IPostDownloader
{
    public Task<string> Download(Request request, CancellationToken token);
    public Task<bool> Verify();
}
