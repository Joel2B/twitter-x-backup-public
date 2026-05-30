using Backup.Application.Posts.Ports;

namespace Backup.Application.Posts;

public sealed class PostRuntimeService(IPostExecutionService postExecutionService) : IPostRuntimeService
{
    private readonly IPostExecutionService _postExecutionService = postExecutionService;

    public async Task Download(IPostDownloadRuntimeCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        command.OnDownloadStarting();
        await _postExecutionService.Download(new DownloadExecution(command));
    }

    public async Task Recover(IPostRecoveryRuntimeCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        command.OnRecoveryStarting();
        await _postExecutionService.Recover(new RecoveryExecution(command));
    }

    private sealed class DownloadExecution(IPostDownloadRuntimeCommand command) : IPostDownloadExecution
    {
        public Task Download() => command.RunDownload();

        public Task Prune() => command.RunPrune();
    }

    private sealed class RecoveryExecution(IPostRecoveryRuntimeCommand command)
        : IPostRecoveryExecution
    {
        public Task Recover() => command.RunRecovery();
    }
}
