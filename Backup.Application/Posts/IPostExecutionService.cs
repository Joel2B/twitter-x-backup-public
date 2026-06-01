using Backup.Application.Posts.Ports;

namespace Backup.Application.Posts;

public interface IPostExecutionService
{
    Task Download(IPostDownloadExecution execution, CancellationToken cancellationToken = default);
    Task Recover(IPostRecoveryExecution execution, CancellationToken cancellationToken = default);
}
