namespace Backup.Application.Posts.Ports;

public interface IPostDownloadRuntimeCommand
{
    void OnDownloadStarting();
    Task RunDownload();
    Task RunPrune();
}

