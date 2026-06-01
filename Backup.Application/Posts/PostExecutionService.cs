using Backup.Application.Posts.Ports;

namespace Backup.Application.Posts;

public class PostExecutionService : IPostExecutionService
{
    public async Task Download(
        IPostDownloadExecution execution,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(execution);
        cancellationToken.ThrowIfCancellationRequested();

        await execution.Download(cancellationToken);
        await execution.Prune(cancellationToken);
    }

    public async Task Recover(
        IPostRecoveryExecution execution,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(execution);
        cancellationToken.ThrowIfCancellationRequested();

        await execution.Recover(cancellationToken);
    }
}
