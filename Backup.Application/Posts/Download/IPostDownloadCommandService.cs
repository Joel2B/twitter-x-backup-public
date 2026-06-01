using Backup.Application.Posts.Ports;

namespace Backup.Application.Posts;

public interface IPostDownloadCommandService
{
    Task Execute(IPostDownloadCommand command, CancellationToken cancellationToken);
}
