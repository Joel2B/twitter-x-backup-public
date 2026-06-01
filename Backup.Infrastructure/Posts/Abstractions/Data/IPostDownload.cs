using Backup.Infrastructure.Models.Config.Api;

namespace Backup.Infrastructure.Posts.Abstractions.Data;

public interface IPostDownload
{
    public Task Download(
        IPostDomainData postData,
        ApiContext context,
        CancellationToken cancellationToken = default
    );
}
