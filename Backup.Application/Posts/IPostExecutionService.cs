using Backup.Application.Posts.Ports;

namespace Backup.Application.Posts;

public interface IPostExecutionService
{
    Task Download(IPostDownloadExecution execution);
    Task Recover(IPostRecoveryExecution execution);
}
