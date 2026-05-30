using Backup.Application.Posts.Ports;

namespace Backup.Application.Posts;

public interface IPostRuntimeService
{
    Task Download(IPostDownloadRuntimeCommand command);
    Task Recover(IPostRecoveryRuntimeCommand command);
}
