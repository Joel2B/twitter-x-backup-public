namespace Backup.Application.Posts.Ports;

public interface IPostDownloadExecution
{
    Task Download();
    Task Prune();
}
