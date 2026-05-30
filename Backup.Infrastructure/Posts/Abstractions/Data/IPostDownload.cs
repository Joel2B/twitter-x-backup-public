using Backup.Infrastructure.Posts.Abstractions.Data;
using Backup.Infrastructure.Models.Config.Api;

namespace Backup.Infrastructure.Models.Posts;

public interface IPostDownload
{
    public Task Download(IPostDomainData postData, ApiContext context);
}
