using Backup.Application.Posts.Ports;

namespace Backup.Application.Posts;

public class PostExecutionService : IPostExecutionService
{
    public async Task Download(IPostDownloadExecution execution)
    {
        ArgumentNullException.ThrowIfNull(execution);

        await execution.Download();
        await execution.Prune();
    }

    public async Task Recover(IPostRecoveryExecution execution)
    {
        ArgumentNullException.ThrowIfNull(execution);

        await execution.Recover();
    }
}
