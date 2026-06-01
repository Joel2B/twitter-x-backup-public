using Backup.Application.Posts.Ports;

namespace Backup.Application.Posts;

public interface IPostRuntimeService
{
    Task Download(
        IPostDownloadRuntimeCommand command,
        CancellationToken cancellationToken = default
    );
    Task Recover(
        IPostRecoveryRuntimeCommand command,
        CancellationToken cancellationToken = default
    );
}
