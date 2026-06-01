using Backup.Application.Posts.Ports;

namespace Backup.Application.Posts;

public sealed class PostRuntimeService(IPostExecutionService postExecutionService)
    : IPostRuntimeService
{
    private readonly IPostExecutionService _postExecutionService = postExecutionService;

    public async Task Download(
        IPostDownloadRuntimeCommand command,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        command.OnDownloadStarting();
        await _postExecutionService.Download(new DownloadExecution(command), cancellationToken);
    }

    public async Task Recover(
        IPostRecoveryRuntimeCommand command,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        command.OnRecoveryStarting();
        await _postExecutionService.Recover(new RecoveryExecution(command), cancellationToken);
    }

    private sealed class DownloadExecution(IPostDownloadRuntimeCommand command)
        : IPostDownloadExecution
    {
        public Task Download(CancellationToken cancellationToken = default) =>
            command.RunDownload(cancellationToken);

        public Task Prune(CancellationToken cancellationToken = default) =>
            command.RunPrune(cancellationToken);
    }

    private sealed class RecoveryExecution(IPostRecoveryRuntimeCommand command)
        : IPostRecoveryExecution
    {
        public Task Recover(CancellationToken cancellationToken = default) =>
            command.RunRecovery(cancellationToken);
    }
}
