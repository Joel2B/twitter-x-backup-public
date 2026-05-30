using Backup.Application.Posts.Ports;

namespace Backup.Application.Posts;

public interface IPostDownloadOrchestrationService
{
    Task Run(IPostDownloadSession session, CancellationToken cancellationToken);
}

