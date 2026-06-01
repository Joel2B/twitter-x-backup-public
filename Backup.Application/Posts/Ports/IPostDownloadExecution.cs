namespace Backup.Application.Posts.Ports;

public interface IPostDownloadExecution
{
    Task Download(CancellationToken cancellationToken = default);
    Task Prune(CancellationToken cancellationToken = default);
}
