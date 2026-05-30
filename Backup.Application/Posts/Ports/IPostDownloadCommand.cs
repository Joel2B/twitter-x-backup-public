namespace Backup.Application.Posts.Ports;

public interface IPostDownloadCommand
{
    Task<int> GetLoadedCount();
    IPostDownloadSession CreateSession();
    void OnLoadedCount(int count);
    void OnError(Exception exception);
    Task PruneLogs();
    Task SavePosts();
}
