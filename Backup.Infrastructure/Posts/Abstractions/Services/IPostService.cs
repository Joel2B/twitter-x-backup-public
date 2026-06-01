using Backup.Infrastructure.Models.Config.Api;

namespace Backup.Infrastructure.Posts.Abstractions.Services;

public interface IPostService
{
    public Task Recover(UsersContext context, CancellationToken cancellationToken = default);
    public Task Download(ApiContext context, CancellationToken cancellationToken = default);
}
