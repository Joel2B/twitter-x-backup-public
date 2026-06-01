namespace Backup.Application.Posts.Ports;

public interface IPostDownloadRuntimeCommand
{
    void OnDownloadStarting();
    Task RunDownload(CancellationToken cancellationToken = default);
    Task RunPrune(CancellationToken cancellationToken = default);
}
